using System.Globalization;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Contracts.IntegrationEvents;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Tenants.Contracts;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Financeiro.Application.Cobrancas;

/// <summary>
/// Rotinas diárias da cobrança, sempre no tenant de uma funerária (os jobs chamam uma vez por funerária, cada uma no próprio
/// escopo). Uma falha numa assinatura não interrompe as demais.
/// </summary>
public sealed partial class RotinasDeCobranca(
    IContaPagamentoRepository contas,
    IAssinaturaRepository assinaturas,
    IFaturaRepository faturas,
    IPagamentoGateway gateway,
    IFinanceiroUnitOfWork unitOfWork,
    EspelhoDeFaturas espelho,
    ProvisionamentoAssinaturas provisionamento,
    AvisosDeCobranca avisos,
    ITenantsApi tenants,
    IClientesApi clientes,
    IEnvioEmail email,
    IEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<RotinasDeCobranca> logger)
{
    /// <summary>Depois de tantos dias suspenso, o contrato é cancelado (DEC-12).</summary>
    public const int DiasAteCancelar = 90;

    /// <summary>Antecedência do aviso de reajuste (COB-08).</summary>
    public const int DiasDeAvisoDoReajuste = 30;

    private DateOnly Hoje => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    /// <summary>Corrige eventos perdidos (COB-02) e cria as assinaturas que esperavam a conta da funerária.</summary>
    public async Task ReconciliarAsync(Guid idTenant, CancellationToken cancellationToken)
    {
        if (await contas.ObterDaFunerariaAsync(cancellationToken) is not ContaPagamento conta)
        {
            return;
        }

        await provisionamento.AtualizarContaAsync(conta, cancellationToken);
        await provisionamento.ProvisionarPendentesAsync(cancellationToken);

        foreach (Assinatura assinatura in await assinaturas.ListarPorStatusAsync(StatusAssinatura.Ativa, cancellationToken))
        {
            try
            {
                foreach (FaturaGateway dados in await gateway.ListarFaturasAsync(
                             conta.IdStripeAccount, assinatura.IdStripeCustomer!, assinatura.IdStripeSubscription!, assinatura.IdContrato, cancellationToken))
                {
                    Fatura? local = await faturas.ObterPorStripeAsync(dados.IdInvoice, cancellationToken);
                    if (local is null || (dados.Situacao == SituacaoFaturaStripe.Paga) != (local.Status == StatusFatura.Paga))
                    {
                        LogDivergencia(logger, dados.IdInvoice, assinatura.IdContrato);
                    }

                    await espelho.EspelharAsync(dados, assinatura, cancellationToken);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFalhaReconciliacao(logger, e, assinatura.IdContrato);
            }
        }
    }

    /// <summary>
    /// Marca as faturas vencidas e, passada a tolerância em dias úteis, suspende o contrato (COB-03, DEC-12). Fatura em
    /// compensação não conta. Depois de 90 dias suspenso, o contrato é cancelado.
    /// </summary>
    public async Task AplicarInadimplenciaAsync(Guid idTenant, CancellationToken cancellationToken)
    {
        ParametrosCobrancaDto? parametros = await tenants.ObterParametrosCobrancaAsync(idTenant, cancellationToken);
        if (parametros is null)
        {
            return;
        }

        DateOnly hoje = Hoje;
        IReadOnlyList<Fatura> abertas = await faturas.ListarEmAbertoAsync(cancellationToken);
        foreach (Fatura fatura in abertas)
        {
            fatura.MarcarVencida(hoje);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (IGrouping<Guid, Fatura> doContrato in abertas.Where(f => f.Status == StatusFatura.Vencida).GroupBy(f => f.IdContrato))
        {
            if (await assinaturas.ObterDoContratoAsync(doContrato.Key, cancellationToken) is not { Status: not StatusAssinatura.Cancelada } assinatura)
            {
                continue;
            }

            if (doContrato.Any(f => hoje > CicloCobranca.FimDaTolerancia(f.DataVencimento, parametros.ToleranciaDias)))
            {
                assinatura.MarcarInadimplente(hoje);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (assinatura.InadimplenteDesde is DateOnly desde && hoje.DayNumber - desde.DayNumber > DiasAteCancelar)
            {
                await eventBus.PublishAsync(
                    new AssinaturaEncerradaIntegrationEvent(idTenant, assinatura.IdContrato, $"Mais de {DiasAteCancelar} dias suspenso por falta de pagamento."),
                    cancellationToken);
            }
        }
    }

    /// <summary>Lembretes antes, no dia e depois do vencimento, com o link de pagamento (COB-04). Cada um só uma vez.</summary>
    public async Task EnviarLembretesAsync(Guid idTenant, CancellationToken cancellationToken)
    {
        ParametrosCobrancaDto? parametros = await tenants.ObterParametrosCobrancaAsync(idTenant, cancellationToken);
        if (parametros is null)
        {
            return;
        }

        HashSet<int> dias = [0, .. parametros.LembretesDiasAntes.Select(d => -d), .. parametros.LembretesDiasDepois];
        DateOnly hoje = Hoje;
        foreach (Fatura fatura in await faturas.ListarEmAbertoAsync(cancellationToken))
        {
            int diasDoVencimento = hoje.DayNumber - fatura.DataVencimento.DayNumber;
            if (fatura.Status == StatusFatura.Processando || !dias.Contains(diasDoVencimento)
                || await faturas.LembreteEnviadoAsync(fatura.Id, diasDoVencimento, cancellationToken)
                || await clientes.ObterTitularAsync(fatura.IdCliente, cancellationToken) is not TitularDto titular)
            {
                continue;
            }

            string quando = diasDoVencimento switch
            {
                < 0 => $"vence em {-diasDoVencimento} dia(s)",
                0 => "vence hoje",
                _ => $"venceu há {diasDoVencimento} dia(s)"
            };
            await email.EnfileirarAsync(
                new EmailSolicitado(ModelosEmail.LembreteFatura, titular.Email, titular.Nome, idTenant, new Dictionary<string, string>
                {
                    ["nome"] = titular.Nome,
                    ["valor"] = fatura.Valor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR")),
                    ["vencimento"] = fatura.DataVencimento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    ["quando"] = quando,
                    ["link"] = fatura.LinkPagamento ?? string.Empty
                }),
                cancellationToken);

            faturas.AdicionarLembrete(LembreteFatura.Criar(idTenant, fatura.Id, diasDoVencimento));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Reajuste no aniversário (COB-08), com aviso 30 dias antes. Idempotente: o aniversário só anda depois de aplicado, e a
    /// assinatura muda sem cobrança proporcional.
    /// </summary>
    public async Task AplicarReajustesAsync(Guid idTenant, CancellationToken cancellationToken)
    {
        ParametrosCobrancaDto? parametros = await tenants.ObterParametrosCobrancaAsync(idTenant, cancellationToken);
        if (parametros is null || parametros.ReajusteAnualPercentual <= 0
            || await contas.ObterDaFunerariaAsync(cancellationToken) is not ContaPagamento conta)
        {
            return;
        }

        DateOnly hoje = Hoje;
        foreach (Assinatura a in await assinaturas.ListarPorStatusAsync(StatusAssinatura.Ativa, cancellationToken))
        {
            try
            {
                DateOnly aniversario = a.ProximoReajuste;
                decimal valorPlano = CicloCobranca.Reajustar(a.ValorPlano, parametros.ReajusteAnualPercentual);
                decimal valorDependente = CicloCobranca.Reajustar(a.ValorPorDependente, parametros.ReajusteAnualPercentual);

                if (hoje >= aniversario)
                {
                    await gateway.ReajustarAsync(
                        conta.IdStripeAccount,
                        new ReajusteAssinatura(
                            a.IdContrato, a.IdPlano, a.NomePlano, a.Periodicidade, a.IdStripeSubscription!, a.IdItemPlano!, a.IdItemDependentes!,
                            a.QuantidadeDependentes, valorPlano, valorDependente, aniversario.Year),
                        cancellationToken);
                    a.Reajustar(valorPlano, valorDependente, aniversario);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                else if (hoje >= aniversario.AddDays(-DiasDeAvisoDoReajuste) && a.AvisoReajustePara != aniversario)
                {
                    await avisos.ReajusteAsync(a, valorPlano + (valorDependente * a.QuantidadeDependentes), aniversario, cancellationToken);
                    a.RegistrarAvisoReajuste(aniversario);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFalhaReajuste(logger, e, a.IdContrato);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconciliação: fatura {IdInvoice} do contrato {IdContrato} divergia do Stripe")]
    private static partial void LogDivergencia(ILogger logger, string idInvoice, Guid idContrato);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reconciliação falhou para o contrato {IdContrato}")]
    private static partial void LogFalhaReconciliacao(ILogger logger, Exception e, Guid idContrato);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reajuste falhou para o contrato {IdContrato}")]
    private static partial void LogFalhaReajuste(ILogger logger, Exception e, Guid idContrato);
}
