using System.Globalization;
using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Contracts.IntegrationEvents;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Funeraria.Modules.Identidade.Contracts;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Tenants.Contracts;

namespace Funeraria.Modules.Financeiro.Application.Cobrancas;

/// <summary>Faturas do contrato (COB-01). O cliente só vê as próprias; a equipe, todas da funerária.</summary>
public sealed record ListarFaturasDoContratoQuery(Guid IdContrato) : IQuery<IReadOnlyList<FaturaResponse>>;

public sealed record ObterFaturaQuery(Guid IdFatura) : IQuery<FaturaResponse>;

/// <summary>Pagamento recebido fora do Stripe, como dinheiro no balcão (COB-07). Exige a forma e uma observação.</summary>
public sealed record BaixaManualCommand(Guid IdFatura, FormaPagamento? FormaPagamento, string? Observacao) : ICommand<FaturaResponse>;

/// <param name="IdFatura">Fatura.</param>
/// <param name="IdContrato">Contrato.</param>
/// <param name="Competencia">Início do período cobrado.</param>
/// <param name="Valor">Valor da fatura.</param>
/// <param name="ValorMultaJuros">Multa e juros já lançados (COB-09).</param>
/// <param name="EncargosEstimados">Em atraso: quanto de multa e juros entra se pagar hoje (COB-09).</param>
/// <param name="DataVencimento">Vencimento.</param>
/// <param name="Status">PENDENTE, PROCESSANDO, PAGA, VENCIDA ou CANCELADA.</param>
/// <param name="FormaPagamento">CARTAO, PIX, BOLETO ou MANUAL, depois de paga.</param>
/// <param name="LinkPagamento">Página de pagamento hospedada pelo Stripe.</param>
/// <param name="DataPagamento">Quando foi paga.</param>
public sealed record FaturaResponse(
    Guid IdFatura,
    Guid IdContrato,
    DateOnly Competencia,
    decimal Valor,
    decimal ValorMultaJuros,
    decimal EncargosEstimados,
    DateOnly DataVencimento,
    StatusFatura Status,
    FormaPagamento? FormaPagamento,
    string? LinkPagamento,
    DateTime? DataPagamento);

internal sealed class BaixaManualCommandValidator : AbstractValidator<BaixaManualCommand>
{
    public BaixaManualCommandValidator()
    {
        RuleFor(c => c.FormaPagamento).NotNull().WithMessage("Informe a forma de pagamento.")
            .IsInEnum().WithMessage("Forma de pagamento inválida. Use CARTAO, PIX, BOLETO ou MANUAL.");
        RuleFor(c => c.Observacao)
            .NotEmpty().WithMessage("Informe uma observação sobre o recebimento.")
            .MaximumLength(500).WithMessage("Observação deve ter no máximo 500 caracteres.");
    }
}

/// <summary>Monta a resposta com a estimativa de multa e juros da fatura em atraso (COB-09).</summary>
public sealed class ApresentacaoDeFaturas(ITenantsApi tenants, ITenantContext tenantContext, TimeProvider timeProvider)
{
    public async Task<FaturaResponse> MontarAsync(Fatura fatura, CancellationToken cancellationToken) =>
        Montar(fatura, await tenants.ObterParametrosCobrancaAsync(tenantContext.RequiredTenantId, cancellationToken));

    public async Task<IReadOnlyList<FaturaResponse>> MontarAsync(IEnumerable<Fatura> faturas, CancellationToken cancellationToken)
    {
        ParametrosCobrancaDto? parametros = await tenants.ObterParametrosCobrancaAsync(tenantContext.RequiredTenantId, cancellationToken);
        return [.. faturas.Select(f => Montar(f, parametros))];
    }

    private FaturaResponse Montar(Fatura f, ParametrosCobrancaDto? parametros)
    {
        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        decimal estimados = f.Status == StatusFatura.Vencida && f.AtrasoCobravel(hoje)
            ? CicloCobranca.Encargos(
                f.Valor, parametros?.MultaPercentual ?? 0, parametros?.JurosMensalPercentual ?? 0, hoje.DayNumber - f.DataVencimento.DayNumber)
            : 0;
        return new FaturaResponse(
            f.Id, f.IdContrato, f.Competencia, f.Valor, f.ValorMultaJuros, estimados, f.DataVencimento, f.Status, f.FormaPagamento, f.LinkPagamento, f.DataPagamento);
    }
}

public sealed class ListarFaturasDoContratoQueryHandler(
    IFaturaRepository faturas,
    IAssinaturaRepository assinaturas,
    ApresentacaoDeFaturas apresentacao,
    ICurrentUser usuario)
    : IQueryHandler<ListarFaturasDoContratoQuery, IReadOnlyList<FaturaResponse>>
{
    public async ValueTask<Result<IReadOnlyList<FaturaResponse>>> Handle(ListarFaturasDoContratoQuery query, CancellationToken cancellationToken)
    {
        Assinatura? assinatura = await assinaturas.ObterDoContratoAsync(query.IdContrato, cancellationToken);
        if (assinatura is null || (usuario.Perfil == Perfis.Cliente && assinatura.IdCliente != usuario.IdCliente))
        {
            return FaturaErros.ContratoNaoEncontrado;
        }

        return Result.Success(await apresentacao.MontarAsync(await faturas.ListarDoContratoAsync(query.IdContrato, cancellationToken), cancellationToken));
    }
}

public sealed class ObterFaturaQueryHandler(IFaturaRepository faturas, ApresentacaoDeFaturas apresentacao, ICurrentUser usuario)
    : IQueryHandler<ObterFaturaQuery, FaturaResponse>
{
    public async ValueTask<Result<FaturaResponse>> Handle(ObterFaturaQuery query, CancellationToken cancellationToken)
    {
        Fatura? fatura = await faturas.ObterAsync(query.IdFatura, cancellationToken);
        return fatura is null || (usuario.Perfil == Perfis.Cliente && fatura.IdCliente != usuario.IdCliente)
            ? FaturaErros.NaoEncontrada
            : await apresentacao.MontarAsync(fatura, cancellationToken);
    }
}

public sealed class BaixaManualCommandHandler(
    IFaturaRepository faturas,
    IContaPagamentoRepository contas,
    IPagamentoGateway gateway,
    IFinanceiroUnitOfWork unitOfWork,
    ApresentacaoDeFaturas apresentacao,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : ICommandHandler<BaixaManualCommand, FaturaResponse>
{
    public async ValueTask<Result<FaturaResponse>> Handle(BaixaManualCommand command, CancellationToken cancellationToken)
    {
        Fatura? fatura = await faturas.ObterAsync(command.IdFatura, cancellationToken);
        if (fatura is null)
        {
            return FaturaErros.NaoEncontrada;
        }

        StatusFatura statusAnterior = fatura.Status;
        Result baixa = fatura.RegistrarBaixaManual(command.FormaPagamento!.Value, command.Observacao!, timeProvider.GetUtcNow().UtcDateTime);
        if (baixa.IsFailure)
        {
            return Result.Failure<FaturaResponse>(baixa.Error);
        }

        // Primeiro o Stripe: se ele recusar, nada é gravado e a fatura continua em aberto nos dois lados.
        if (await contas.ObterDaFunerariaAsync(cancellationToken) is ContaPagamento conta)
        {
            await gateway.MarcarPagaForaAsync(conta.IdStripeAccount, fatura.IdStripeInvoice, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.FaturaBaixaManual, "Fatura", fatura.Id,
            ValoresAnteriores: new { status = statusAnterior.ToString() },
            ValoresNovos: new { status = fatura.Status.ToString(), forma = fatura.FormaPagamento?.ToString(), observacao = fatura.ObservacaoBaixa }),
            cancellationToken);

        return await apresentacao.MontarAsync(fatura, cancellationToken);
    }
}

/// <summary>Cria ou atualiza a fatura local a partir do Stripe (COB-02). Idempotente: <c>id_stripe_invoice</c> é único.</summary>
public sealed class EspelhoDeFaturas(
    IFaturaRepository faturas,
    IAssinaturaRepository assinaturas,
    IFinanceiroUnitOfWork unitOfWork,
    ITenantContext tenantContext)
{
    public async Task<Fatura?> EspelharAsync(FaturaGateway dados, Assinatura? assinatura, CancellationToken cancellationToken)
    {
        assinatura ??= dados.IdSubscription is string sub
            ? await assinaturas.ObterPorSubscriptionAsync(sub, cancellationToken)
            : dados.IdContrato is Guid idContrato ? await assinaturas.ObterDoContratoAsync(idContrato, cancellationToken) : null;
        if (assinatura is null)
        {
            return null;
        }

        var espelhada = new FaturaEspelhada(
            dados.IdInvoice, dados.Competencia, dados.Valor, dados.DataVencimento, dados.Situacao, dados.LinkPagamento, dados.PagaEm,
            dados.FormaPagamento, Avulsa: dados.IdSubscription is null);

        Fatura? fatura = await faturas.ObterPorStripeAsync(dados.IdInvoice, cancellationToken);
        if (fatura is null)
        {
            fatura = Fatura.Espelhar(tenantContext.RequiredTenantId, assinatura.IdContrato, assinatura.IdCliente, espelhada);
            faturas.Adicionar(fatura);
        }
        else
        {
            fatura.Atualizar(espelhada);
        }

        assinatura.AtualizarVigencia(dados.FimDoPeriodo);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return fatura;
    }
}

/// <summary>Trata cada evento recebido do Stripe (COB-06), já no tenant da conta conectada.</summary>
public sealed class ProcessamentoEventosStripe(
    IPagamentoGateway gateway,
    IAssinaturaRepository assinaturas,
    IFaturaRepository faturas,
    IFinanceiroUnitOfWork unitOfWork,
    EspelhoDeFaturas espelho,
    ProvisionamentoAssinaturas provisionamento,
    AvisosDeCobranca avisos,
    IEventBus eventBus,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
{
    public async Task ProcessarAsync(EventoGateway evento, ContaPagamento conta, CancellationToken cancellationToken)
    {
        DateOnly hoje = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        switch (evento.Tipo)
        {
            case TiposEventoStripe.ContaAtualizada:
                await provisionamento.AtualizarContaAsync(conta, cancellationToken);
                break;

            case TiposEventoStripe.FaturaCriada or TiposEventoStripe.FaturaFinalizada or TiposEventoStripe.FaturaAtualizada
                or TiposEventoStripe.FaturaPaga or TiposEventoStripe.FaturaCancelada:
                await EspelharAsync(conta, evento.IdObjeto, cancellationToken);
                break;

            case TiposEventoStripe.FaturaFalhou:
                if (await EspelharAsync(conta, evento.IdObjeto, cancellationToken) is Fatura comFalha)
                {
                    await avisos.PagamentoFalhouAsync(comFalha, cancellationToken);
                }

                break;

            case TiposEventoStripe.PagamentoProcessando or TiposEventoStripe.PagamentoFalhou:
                if (evento.IdObjeto is string pagamento
                    && await gateway.ObterFaturaDoPagamentoAsync(conta.IdStripeAccount, pagamento, cancellationToken) is string idInvoice
                    && await faturas.ObterPorStripeAsync(idInvoice, cancellationToken) is Fatura fatura)
                {
                    if (evento.Tipo == TiposEventoStripe.PagamentoProcessando)
                    {
                        fatura.MarcarProcessando(hoje);
                    }
                    else
                    {
                        fatura.PagamentoFalhou(hoje);
                    }

                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                break;

            case TiposEventoStripe.AssinaturaRemovida:
                if (evento.IdObjeto is string idSub && await assinaturas.ObterPorSubscriptionAsync(idSub, cancellationToken) is Assinatura removida
                    && removida.Status != StatusAssinatura.Cancelada)
                {
                    removida.Encerrar();
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await eventBus.PublishAsync(
                        new AssinaturaEncerradaIntegrationEvent(tenantContext.RequiredTenantId, removida.IdContrato, "Assinatura encerrada no Stripe."),
                        cancellationToken);
                }

                break;
        }
    }

    private async Task<Fatura?> EspelharAsync(ContaPagamento conta, string? idInvoice, CancellationToken cancellationToken) =>
        idInvoice is not null && await gateway.ObterFaturaAsync(conta.IdStripeAccount, idInvoice, cancellationToken) is FaturaGateway dados
            ? await espelho.EspelharAsync(dados, null, cancellationToken)
            : null;
}

/// <summary>
/// Fatura paga: lança multa e juros, se o atraso for do cliente (COB-09), e põe o contrato em dia quando não sobra fatura
/// vencida (COB-03). Roda pelo outbox, então pode rodar de novo: a chave de idempotência evita cobrar duas vezes.
/// </summary>
internal sealed class FaturaPagaDomainEventHandler(
    IFaturaRepository faturas,
    IAssinaturaRepository assinaturas,
    IContaPagamentoRepository contas,
    IPagamentoGateway gateway,
    IFinanceiroUnitOfWork unitOfWork,
    ITenantsApi tenants,
    IEventBus eventBus)
    : IDomainEventHandler<FaturaPagaDomainEvent>
{
    public async Task Handle(FaturaPagaDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await faturas.ObterAsync(domainEvent.IdFatura, cancellationToken) is not Fatura fatura
            || await assinaturas.ObterDoContratoAsync(fatura.IdContrato, cancellationToken) is not Assinatura assinatura)
        {
            return;
        }

        await LancarEncargosAsync(fatura, assinatura, cancellationToken);

        IReadOnlyList<Fatura> doContrato = await faturas.ListarDoContratoAsync(fatura.IdContrato, cancellationToken);
        if (doContrato.Any(f => f.Status == StatusFatura.Vencida))
        {
            return;
        }

        if (assinatura.Inadimplente)
        {
            assinatura.MarcarEmDia();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await eventBus.PublishAsync(new ContratoEmDiaIntegrationEvent(fatura.TenantId, fatura.IdContrato), cancellationToken);
        }
    }

    private async Task LancarEncargosAsync(Fatura fatura, Assinatura assinatura, CancellationToken cancellationToken)
    {
        if (fatura.ValorMultaJuros > 0 || fatura.DataPagamento is not DateTime pagaEm || fatura.FormaPagamento == FormaPagamento.Manual)
        {
            return;
        }

        DateOnly dataPagamento = DateOnly.FromDateTime(pagaEm);
        ParametrosCobrancaDto? parametros = await tenants.ObterParametrosCobrancaAsync(fatura.TenantId, cancellationToken);
        if (!fatura.AtrasoCobravel(dataPagamento) || parametros is null || assinatura.IdStripeCustomer is null
            || await contas.ObterDaFunerariaAsync(cancellationToken) is not ContaPagamento conta)
        {
            return;
        }

        decimal encargos = CicloCobranca.Encargos(
            fatura.Valor, parametros.MultaPercentual ?? 0, parametros.JurosMensalPercentual ?? 0, dataPagamento.DayNumber - fatura.DataVencimento.DayNumber);
        if (encargos <= 0)
        {
            return;
        }

        var cobranca = new CobrancaExtra(
            fatura.IdContrato, assinatura.IdStripeCustomer,
            $"Multa e juros da fatura vencida em {fatura.DataVencimento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}", encargos, $"encargos-{fatura.Id}");

        // Fatura já emitida não muda de valor: o mensal paga na próxima fatura; o anual, numa avulsa (COB-09).
        if (assinatura.Periodicidade == PeriodicidadeCobranca.Mensal && assinatura.IdStripeSubscription is not null)
        {
            await gateway.AdicionarNaProximaFaturaAsync(conta.IdStripeAccount, cobranca, assinatura.IdStripeSubscription, cancellationToken);
        }
        else
        {
            await gateway.CriarFaturaAvulsaAsync(conta.IdStripeAccount, cobranca, cancellationToken);
        }

        fatura.RegistrarEncargos(encargos);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Suspensão (COB-03): avisa o Contratos, o cliente e a funerária.</summary>
internal sealed class AssinaturaInadimplenteDomainEventHandler(IAssinaturaRepository assinaturas, AvisosDeCobranca avisos, IEventBus eventBus)
    : IDomainEventHandler<AssinaturaInadimplenteDomainEvent>
{
    public async Task Handle(AssinaturaInadimplenteDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await assinaturas.ObterPorIdAsync(domainEvent.IdAssinatura, cancellationToken) is Assinatura a)
        {
            await eventBus.PublishAsync(new ContratoInadimplenteIntegrationEvent(a.TenantId, a.IdContrato), cancellationToken);
            await avisos.ContratoSuspensoAsync(a, cancellationToken);
        }
    }
}

/// <summary>Reativação (COB-03): pendências quitadas, sem nova carência (DEC-12).</summary>
internal sealed class AssinaturaEmDiaDomainEventHandler(IAssinaturaRepository assinaturas, AvisosDeCobranca avisos, IEventBus eventBus)
    : IDomainEventHandler<AssinaturaEmDiaDomainEvent>
{
    public async Task Handle(AssinaturaEmDiaDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await assinaturas.ObterPorIdAsync(domainEvent.IdAssinatura, cancellationToken) is Assinatura a)
        {
            await eventBus.PublishAsync(new ContratoEmDiaIntegrationEvent(a.TenantId, a.IdContrato), cancellationToken);
            await avisos.ContratoReativadoAsync(a, cancellationToken);
        }
    }
}

internal sealed class AssinaturaReajustadaDomainEventHandler(IAssinaturaRepository assinaturas, IEventBus eventBus)
    : IDomainEventHandler<AssinaturaReajustadaDomainEvent>
{
    public async Task Handle(AssinaturaReajustadaDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await assinaturas.ObterPorIdAsync(domainEvent.IdAssinatura, cancellationToken) is Assinatura a)
        {
            await eventBus.PublishAsync(new ContratoReajustadoIntegrationEvent(a.TenantId, a.IdContrato, a.ValorPlano, a.ValorPorDependente), cancellationToken);
        }
    }
}

/// <summary>Avisos ao cliente e à funerária sobre a cobrança (COB-03, COB-06).</summary>
public sealed class AvisosDeCobranca(INotificacoes notificacoes, IClientesApi clientes, IIdentidadeApi identidade, ITenantContext tenantContext)
{
    public async Task PagamentoFalhouAsync(Fatura fatura, CancellationToken cancellationToken)
    {
        if (await clientes.ObterTitularAsync(fatura.IdCliente, cancellationToken) is TitularDto titular)
        {
            await notificacoes.NotificarAsync(new NovaNotificacao(
                [titular.IdUsuario], fatura.TenantId, "PAGAMENTO_FALHOU", "Não conseguimos confirmar seu pagamento",
                "Confira a forma de pagamento e tente de novo pelo link da fatura.", $"/faturas/{fatura.Id}"), cancellationToken);
        }
    }

    public async Task ContratoSuspensoAsync(Assinatura a, CancellationToken cancellationToken) =>
        await AvisarAsync(a, "CONTRATO_SUSPENSO", "Plano suspenso por falta de pagamento",
            "A cobertura fica suspensa até a pendência ser quitada.", cancellationToken);

    public async Task ContratoReativadoAsync(Assinatura a, CancellationToken cancellationToken) =>
        await AvisarAsync(a, "CONTRATO_REATIVADO", "Plano reativado", "A pendência foi quitada e a cobertura voltou a valer.", cancellationToken);

    public async Task ReajusteAsync(Assinatura a, decimal novoValor, DateOnly aniversario, CancellationToken cancellationToken)
    {
        if (await clientes.ObterTitularAsync(a.IdCliente, cancellationToken) is TitularDto titular)
        {
            string valor = novoValor.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
            string data = aniversario.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            await notificacoes.NotificarAsync(new NovaNotificacao(
                [titular.IdUsuario], a.TenantId, "REAJUSTE_ANUAL", "Reajuste anual do seu plano",
                $"A partir de {data}, o valor passa para R$ {valor}.", $"/contratos/{a.IdContrato}"), cancellationToken);
        }
    }

    private async Task AvisarAsync(Assinatura a, string tipo, string titulo, string mensagem, CancellationToken cancellationToken)
    {
        var destinatarios = new List<Guid>(await identidade.ListarGestoresAtivosAsync(tenantContext.RequiredTenantId, cancellationToken));
        if (await clientes.ObterTitularAsync(a.IdCliente, cancellationToken) is TitularDto titular)
        {
            destinatarios.Add(titular.IdUsuario);
        }

        await notificacoes.NotificarAsync(
            new NovaNotificacao(destinatarios, a.TenantId, tipo, titulo, mensagem, $"/contratos/{a.IdContrato}"), cancellationToken);
    }
}
