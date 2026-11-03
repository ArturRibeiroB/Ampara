using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Contracts.IntegrationEvents;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Funeraria.Modules.Financeiro.Domain.Plataforma;
using Funeraria.Modules.Tenants.Contracts;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Financeiro.Application.Plataforma;

/// <summary>
/// Cobrança das funerárias pela plataforma (DEC-09), no Stripe Billing da conta da <b>própria plataforma</b> (não na conta
/// conectada). A implementação real é o Stripe; os testes usam uma fake.
/// </summary>
public interface ICobrancaPlataformaGateway
{
    Task<string> CriarClienteAsync(Guid idTenant, string nome, string email, CancellationToken cancellationToken);

    /// <summary>Assinatura mensal por fatura enviada (cartão ou boleto). A primeira fatura sai na hora.</summary>
    Task<string> CriarAssinaturaAsync(Guid idTenant, string idCustomer, decimal valorMensal, CancellationToken cancellationToken);

    /// <summary>Novo valor a partir da próxima fatura, sem cobrança proporcional.</summary>
    Task AlterarValorAsync(Guid idTenant, string idSubscription, decimal valorMensal, CancellationToken cancellationToken);

    Task<FaturaPlataforma?> ObterFaturaAsync(string idInvoice, CancellationToken cancellationToken);
}

/// <summary>Valores usados na primeira vez que a configuração da plataforma é lida (seção <c>Stripe</c>).</summary>
public sealed record ValoresIniciaisPlataforma(decimal ValorMensalidade, decimal TaxaPercentual);

/// <summary>
/// Taxa da plataforma de uma conta conectada: a da funerária, se o ADMIN definiu uma, senão a padrão da plataforma. Usada
/// pelo gateway ao criar assinaturas e faturas dos clientes.
/// </summary>
public sealed class TaxaDaPlataforma(IContaPagamentoRepository contas, ConfiguracaoDaPlataforma configuracao)
{
    public async Task<decimal> DaContaAsync(string idStripeAccount, CancellationToken cancellationToken)
    {
        ContaPagamento? conta = await contas.ObterPorIdStripeAsync(idStripeAccount, cancellationToken);
        return conta?.TaxaPlataformaPercentual ?? (await configuracao.ObterAsync(cancellationToken)).TaxaPercentual;
    }
}

/// <summary>Lê a configuração da plataforma; na primeira vez, grava os valores iniciais da configuração da API.</summary>
public sealed class ConfiguracaoDaPlataforma(IMensalidadeRepository repositorio, IFinanceiroUnitOfWork unitOfWork, ValoresIniciaisPlataforma iniciais)
{
    public async Task<ConfiguracaoPlataforma> ObterAsync(CancellationToken cancellationToken)
    {
        ConfiguracaoPlataforma? atual = await repositorio.ObterConfiguracaoAsync(cancellationToken);
        if (atual is not null)
        {
            return atual;
        }

        ConfiguracaoPlataforma nova = ConfiguracaoPlataforma.Criar(iniciais.ValorMensalidade, iniciais.TaxaPercentual);
        repositorio.AdicionarConfiguracao(nova);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return nova;
    }
}

/// <summary>Assinatura, espelho das faturas e valor da mensalidade de cada funerária (DEC-09).</summary>
public sealed partial class CobrancaDaPlataforma(
    IMensalidadeRepository mensalidades,
    ICobrancaPlataformaGateway gateway,
    ConfiguracaoDaPlataforma configuracao,
    IFinanceiroUnitOfWork unitOfWork,
    ITenantsApi tenants,
    TimeProvider timeProvider,
    ILogger<CobrancaDaPlataforma> logger)
{
    /// <summary>A conta de pagamentos da funerária ficou pronta: começa a mensalidade. Idempotente.</summary>
    public async Task AssinarAsync(Guid idTenant, CancellationToken cancellationToken)
    {
        if ((await mensalidades.ListarTodasAsync(cancellationToken)).Any(m => m.TenantId == idTenant))
        {
            return;
        }

        ContatoFunerariaDto funeraria = await tenants.ObterContatoAsync(idTenant, cancellationToken)
            ?? throw new InvalidOperationException($"Funerária {idTenant} não encontrada.");
        decimal valor = (await configuracao.ObterAsync(cancellationToken)).ValorMensalidade;

        string idCustomer = await gateway.CriarClienteAsync(idTenant, funeraria.RazaoSocial, funeraria.EmailContato, cancellationToken);
        string idSubscription = await gateway.CriarAssinaturaAsync(idTenant, idCustomer, valor, cancellationToken);
        mensalidades.Adicionar(MensalidadePlataforma.Criar(idTenant, idCustomer, idSubscription, valor));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogAssinada(logger, idTenant, valor);
    }

    /// <summary>Evento da conta da plataforma (sem conta conectada): fatura da mensalidade ou assinatura encerrada.</summary>
    public async Task ProcessarAsync(EventoGateway evento, CancellationToken cancellationToken)
    {
        switch (evento.Tipo)
        {
            case TiposEventoStripe.FaturaCriada or TiposEventoStripe.FaturaFinalizada or TiposEventoStripe.FaturaAtualizada
                or TiposEventoStripe.FaturaPaga or TiposEventoStripe.FaturaFalhou or TiposEventoStripe.FaturaCancelada:
                if (evento.IdObjeto is string idInvoice
                    && await gateway.ObterFaturaAsync(idInvoice, cancellationToken) is { IdSubscription: string idSub } fatura
                    && await mensalidades.ObterPorSubscriptionAsync(idSub, cancellationToken) is MensalidadePlataforma mensalidade)
                {
                    mensalidade.RegistrarFatura(fatura, Hoje());
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                break;

            case TiposEventoStripe.AssinaturaRemovida:
                if (evento.IdObjeto is string idSubscription
                    && await mensalidades.ObterPorSubscriptionAsync(idSubscription, cancellationToken) is MensalidadePlataforma encerrada)
                {
                    encerrada.Cancelar();
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                break;
        }
    }

    /// <summary>
    /// Rotina diária: começa a mensalidade de quem ficou sem (a criação na liberação da conta falhou), aplica o valor atual
    /// da plataforma às assinaturas e marca como inadimplente quem passou do vencimento sem pagar.
    /// </summary>
    public async Task SincronizarAsync(IReadOnlyCollection<Guid> funerariasProntas, CancellationToken cancellationToken)
    {
        IReadOnlyList<MensalidadePlataforma> todas = await mensalidades.ListarTodasAsync(cancellationToken);
        foreach (Guid idTenant in funerariasProntas.Where(t => todas.All(m => m.TenantId != t)))
        {
            try
            {
                await AssinarAsync(idTenant, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFalha(logger, e, idTenant);
            }
        }

        decimal valor = (await configuracao.ObterAsync(cancellationToken)).ValorMensalidade;
        foreach (MensalidadePlataforma mensalidade in todas.Where(m => m.Status != StatusMensalidade.Cancelada))
        {
            if (mensalidade.Valor != valor)
            {
                await gateway.AlterarValorAsync(mensalidade.TenantId, mensalidade.IdStripeSubscription, valor, cancellationToken);
                mensalidade.AlterarValor(valor);
            }

            if (mensalidade is { SituacaoUltimaFatura: SituacaoFaturaStripe.Aberta, VencimentoUltimaFatura: DateOnly vencimento }
                && vencimento < Hoje()
                && await gateway.ObterFaturaAsync(mensalidade.IdUltimaFatura!, cancellationToken) is FaturaPlataforma atual)
            {
                mensalidade.RegistrarFatura(atual, Hoje());
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    [LoggerMessage(Level = LogLevel.Information, Message = "Mensalidade da plataforma iniciada para a funerária {IdTenant}: {Valor}")]
    private static partial void LogAssinada(ILogger logger, Guid idTenant, decimal valor);

    [LoggerMessage(Level = LogLevel.Error, Message = "Não foi possível iniciar a mensalidade da plataforma da funerária {IdTenant}")]
    private static partial void LogFalha(ILogger logger, Exception e, Guid idTenant);
}

/// <summary>Mensalidade da própria funerária, para o gestor (DEC-09).</summary>
public sealed record ObterMensalidadeQuery : IQuery<MensalidadeResponse>;

/// <summary>Mensalidades de todas as funerárias, para o ADMIN (DEC-09).</summary>
public sealed record ListarMensalidadesQuery : IQuery<IReadOnlyList<MensalidadeFunerariaResponse>>;

public sealed record ObterCobrancaPlataformaQuery : IQuery<CobrancaPlataformaResponse>;

/// <summary>
/// O ADMIN muda a mensalidade e a taxa padrão (DEC-09), sem deploy. A mensalidade nova vale a partir da próxima fatura de
/// cada funerária (aplicada pela rotina diária); a taxa vale para as assinaturas e faturas criadas daqui em diante.
/// </summary>
public sealed record AlterarCobrancaPlataformaCommand(decimal? ValorMensalidade, decimal? TaxaPercentual) : ICommand<CobrancaPlataformaResponse>;

/// <summary>Taxa só desta funerária (DEC-09). Nula volta para a padrão da plataforma.</summary>
public sealed record DefinirTaxaDaFunerariaCommand(Guid IdTenant, decimal? TaxaPercentual) : ICommand<TaxaFunerariaResponse>;

/// <param name="Status">ATIVA, INADIMPLENTE ou CANCELADA.</param>
/// <param name="Valor">Valor mensal atual.</param>
/// <param name="UltimaFatura">Fatura mais recente; nula antes da primeira.</param>
/// <param name="UltimoPagamentoEm">Quando a última mensalidade foi paga.</param>
public sealed record MensalidadeResponse(string Status, decimal Valor, FaturaMensalidadeResponse? UltimaFatura, DateTime? UltimoPagamentoEm)
{
    public static MensalidadeResponse De(MensalidadePlataforma m) => new(
        NomeStatus(m.Status),
        m.Valor,
        m.IdUltimaFatura is null
            ? null
            : new FaturaMensalidadeResponse(m.ValorUltimaFatura ?? 0, m.VencimentoUltimaFatura, NomeSituacao(m.SituacaoUltimaFatura), m.LinkPagamento),
        m.UltimoPagamentoEm);

    internal static string NomeStatus(StatusMensalidade status) => status switch
    {
        StatusMensalidade.Ativa => "ATIVA",
        StatusMensalidade.Inadimplente => "INADIMPLENTE",
        _ => "CANCELADA"
    };

    private static string? NomeSituacao(SituacaoFaturaStripe? situacao) => situacao switch
    {
        SituacaoFaturaStripe.Aberta => "ABERTA",
        SituacaoFaturaStripe.Paga => "PAGA",
        SituacaoFaturaStripe.Cancelada => "CANCELADA",
        _ => null
    };
}

/// <param name="Valor">Valor da fatura.</param>
/// <param name="Vencimento">Vencimento.</param>
/// <param name="Situacao">ABERTA, PAGA ou CANCELADA.</param>
/// <param name="LinkPagamento">Página de pagamento do Stripe, enquanto aberta.</param>
public sealed record FaturaMensalidadeResponse(decimal Valor, DateOnly? Vencimento, string? Situacao, string? LinkPagamento);

public sealed record MensalidadeFunerariaResponse(Guid IdTenant, string NomeFuneraria, string Status, decimal Valor, DateOnly? VencimentoUltimaFatura, string? SituacaoUltimaFatura);

public sealed record CobrancaPlataformaResponse(decimal ValorMensalidade, decimal TaxaPercentual);

/// <param name="IdTenant">Funerária.</param>
/// <param name="TaxaPercentual">Taxa própria da funerária; nula quando vale a padrão.</param>
/// <param name="TaxaVigente">A que vale para ela agora.</param>
public sealed record TaxaFunerariaResponse(Guid IdTenant, decimal? TaxaPercentual, decimal TaxaVigente);

internal sealed class AlterarCobrancaPlataformaCommandValidator : AbstractValidator<AlterarCobrancaPlataformaCommand>
{
    public AlterarCobrancaPlataformaCommandValidator()
    {
        RuleFor(c => c.ValorMensalidade).NotNull().GreaterThan(0).WithMessage(PlataformaErros.ValorInvalido.Description);
        RuleFor(c => c.TaxaPercentual).NotNull().InclusiveBetween(0, ConfiguracaoPlataforma.TaxaMaxima)
            .WithMessage(PlataformaErros.TaxaInvalida.Description);
    }
}

internal sealed class DefinirTaxaDaFunerariaCommandValidator : AbstractValidator<DefinirTaxaDaFunerariaCommand>
{
    public DefinirTaxaDaFunerariaCommandValidator() =>
        RuleFor(c => c.TaxaPercentual).InclusiveBetween(0, ConfiguracaoPlataforma.TaxaMaxima)
            .When(c => c.TaxaPercentual is not null)
            .WithMessage(PlataformaErros.TaxaInvalida.Description);
}

public sealed class ObterMensalidadeQueryHandler(IMensalidadeRepository mensalidades) : IQueryHandler<ObterMensalidadeQuery, MensalidadeResponse>
{
    public async ValueTask<Result<MensalidadeResponse>> Handle(ObterMensalidadeQuery query, CancellationToken cancellationToken) =>
        await mensalidades.ObterDaFunerariaAsync(cancellationToken) is MensalidadePlataforma m
            ? MensalidadeResponse.De(m)
            : PlataformaErros.MensalidadeNaoEncontrada;
}

public sealed class ListarMensalidadesQueryHandler(IMensalidadeRepository mensalidades, ITenantsApi tenants)
    : IQueryHandler<ListarMensalidadesQuery, IReadOnlyList<MensalidadeFunerariaResponse>>
{
    public async ValueTask<Result<IReadOnlyList<MensalidadeFunerariaResponse>>> Handle(ListarMensalidadesQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<MensalidadePlataforma> todas = await mensalidades.ListarTodasAsync(cancellationToken);
        Dictionary<Guid, string> nomes = (await tenants.ListarAsync([.. todas.Select(m => m.TenantId)], cancellationToken))
            .ToDictionary(f => f.IdTenant, f => f.NomeFantasia);

        return todas
            .Select(m => new MensalidadeFunerariaResponse(
                m.TenantId,
                nomes.GetValueOrDefault(m.TenantId, string.Empty),
                MensalidadeResponse.NomeStatus(m.Status),
                m.Valor,
                m.VencimentoUltimaFatura,
                m.SituacaoUltimaFatura?.ToString().ToUpperInvariant()))
            .OrderBy(m => m.NomeFuneraria, StringComparer.CurrentCulture)
            .ToList();
    }
}

public sealed class ObterCobrancaPlataformaQueryHandler(ConfiguracaoDaPlataforma configuracao)
    : IQueryHandler<ObterCobrancaPlataformaQuery, CobrancaPlataformaResponse>
{
    public async ValueTask<Result<CobrancaPlataformaResponse>> Handle(ObterCobrancaPlataformaQuery query, CancellationToken cancellationToken)
    {
        ConfiguracaoPlataforma atual = await configuracao.ObterAsync(cancellationToken);
        return new CobrancaPlataformaResponse(atual.ValorMensalidade, atual.TaxaPercentual);
    }
}

public sealed class AlterarCobrancaPlataformaCommandHandler(
    ConfiguracaoDaPlataforma configuracao,
    IFinanceiroUnitOfWork unitOfWork,
    IAuditoria auditoria)
    : ICommandHandler<AlterarCobrancaPlataformaCommand, CobrancaPlataformaResponse>
{
    public async ValueTask<Result<CobrancaPlataformaResponse>> Handle(AlterarCobrancaPlataformaCommand command, CancellationToken cancellationToken)
    {
        ConfiguracaoPlataforma atual = await configuracao.ObterAsync(cancellationToken);
        var anterior = new { valor_mensalidade = atual.ValorMensalidade, taxa_percentual = atual.TaxaPercentual };

        Result alterada = atual.Alterar(command.ValorMensalidade!.Value, command.TaxaPercentual!.Value);
        if (alterada.IsFailure)
        {
            return Result.Failure<CobrancaPlataformaResponse>(alterada.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.CobrancaPlataformaAlterada, "ConfiguracaoPlataforma", atual.Id,
            anterior, new { valor_mensalidade = atual.ValorMensalidade, taxa_percentual = atual.TaxaPercentual }), cancellationToken);
        return new CobrancaPlataformaResponse(atual.ValorMensalidade, atual.TaxaPercentual);
    }
}

public sealed class DefinirTaxaDaFunerariaCommandHandler(
    IContaPagamentoRepository contas,
    ConfiguracaoDaPlataforma configuracao,
    IFinanceiroUnitOfWork unitOfWork,
    IAuditoria auditoria)
    : ICommandHandler<DefinirTaxaDaFunerariaCommand, TaxaFunerariaResponse>
{
    public async ValueTask<Result<TaxaFunerariaResponse>> Handle(DefinirTaxaDaFunerariaCommand command, CancellationToken cancellationToken)
    {
        ContaPagamento? conta = await contas.ObterPorFunerariaAsync(command.IdTenant, cancellationToken);
        if (conta is null)
        {
            return Result.Failure<TaxaFunerariaResponse>(FaturaErros.ContaNaoCadastrada);
        }

        decimal? anterior = conta.TaxaPlataformaPercentual;
        Result definida = conta.DefinirTaxa(command.TaxaPercentual);
        if (definida.IsFailure)
        {
            return Result.Failure<TaxaFunerariaResponse>(definida.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.TaxaFunerariaAlterada, "ContaPagamento", conta.Id,
            new { taxa_percentual = anterior }, new { taxa_percentual = conta.TaxaPlataformaPercentual }, command.IdTenant), cancellationToken);

        decimal vigente = conta.TaxaPlataformaPercentual ?? (await configuracao.ObterAsync(cancellationToken)).TaxaPercentual;
        return new TaxaFunerariaResponse(command.IdTenant, conta.TaxaPlataformaPercentual, vigente);
    }
}

/// <summary>Publica a liberação da conta para o Tenants ativar a funerária (DEC-11).</summary>
public sealed class AvisoContaLiberada(IEventBus eventBus)
{
    public Task PublicarAsync(Guid idTenant, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(new ContaPagamentosLiberadaIntegrationEvent(idTenant), cancellationToken);
}
