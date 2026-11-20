using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;
using Funeraria.Modules.Financeiro.Domain.Cobranca;

namespace Funeraria.Modules.Financeiro.Domain.Plataforma;

/// <summary>
/// Quanto a plataforma cobra das funerárias (DEC-09): a mensalidade e a taxa retida em cada pagamento dos clientes. Uma
/// linha só, alterada pelo ADMIN sem deploy. Não é de nenhuma funerária.
/// </summary>
public sealed class ConfiguracaoPlataforma : Entity<Guid>, IAuditableEntity
{
    public const decimal TaxaMaxima = 20m;

    private ConfiguracaoPlataforma(Guid id)
        : base(id)
    {
    }

    private ConfiguracaoPlataforma()
    {
    }

    public decimal ValorMensalidade { get; private set; }

    /// <summary>Percentual retido em cada pagamento, quando a funerária não tem uma taxa própria.</summary>
    public decimal TaxaPercentual { get; private set; }

    public static ConfiguracaoPlataforma Criar(decimal valorMensalidade, decimal taxaPercentual) =>
        new(Guid.CreateVersion7()) { ValorMensalidade = valorMensalidade, TaxaPercentual = taxaPercentual };

    public Result Alterar(decimal valorMensalidade, decimal taxaPercentual)
    {
        if (valorMensalidade <= 0)
        {
            return PlataformaErros.ValorInvalido;
        }

        if (taxaPercentual is < 0 or > TaxaMaxima)
        {
            return PlataformaErros.TaxaInvalida;
        }

        ValorMensalidade = valorMensalidade;
        TaxaPercentual = taxaPercentual;
        return Result.Success();
    }
}

/// <summary>
/// Assinatura da funerária com a plataforma (DEC-09): no Stripe Billing da conta da própria plataforma, com a funerária como
/// cliente, por fatura enviada (cartão ou boleto). Nasce quando a conta de pagamentos da funerária fica pronta.
/// </summary>
public sealed class MensalidadePlataforma : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private MensalidadePlataforma(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private MensalidadePlataforma()
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>Customer na conta da plataforma, não na conta conectada.</summary>
    public string IdStripeCustomer { get; private set; } = null!;

    public string IdStripeSubscription { get; private set; } = null!;

    public StatusMensalidade Status { get; private set; }

    public decimal Valor { get; private set; }

    public string? IdUltimaFatura { get; private set; }

    public decimal? ValorUltimaFatura { get; private set; }

    public DateOnly? VencimentoUltimaFatura { get; private set; }

    public SituacaoFaturaStripe? SituacaoUltimaFatura { get; private set; }

    public string? LinkPagamento { get; private set; }

    public DateTime? UltimoPagamentoEm { get; private set; }

    public static MensalidadePlataforma Criar(Guid tenantId, string idStripeCustomer, string idStripeSubscription, decimal valor) =>
        new(Guid.CreateVersion7(), tenantId)
        {
            IdStripeCustomer = idStripeCustomer,
            IdStripeSubscription = idStripeSubscription,
            Valor = valor,
            Status = StatusMensalidade.Ativa
        };

    /// <summary>Novo valor definido pelo ADMIN, já aplicado no Stripe. Vale a partir da próxima fatura.</summary>
    public void AlterarValor(decimal valor) => Valor = valor;

    /// <summary>
    /// Espelha a fatura vinda do Stripe. Paga deixa em dia; aberta e vencida, inadimplente. Uma fatura mais antiga que a última
    /// conhecida só conta se for a mesma (o pagamento de uma fatura atrasada). Cancelada não muda o status.
    /// </summary>
    public void RegistrarFatura(FaturaPlataforma fatura, DateOnly hoje)
    {
        if (Status == StatusMensalidade.Cancelada)
        {
            return;
        }

        bool mesma = fatura.IdInvoice == IdUltimaFatura;
        if (!mesma && VencimentoUltimaFatura is DateOnly ultima && fatura.Vencimento < ultima)
        {
            if (fatura.Situacao == SituacaoFaturaStripe.Paga)
            {
                UltimoPagamentoEm = fatura.PagaEm ?? UltimoPagamentoEm;
            }

            return;
        }

        IdUltimaFatura = fatura.IdInvoice;
        ValorUltimaFatura = fatura.Valor;
        VencimentoUltimaFatura = fatura.Vencimento;
        SituacaoUltimaFatura = fatura.Situacao;
        LinkPagamento = fatura.Situacao == SituacaoFaturaStripe.Aberta ? fatura.LinkPagamento : null;

        switch (fatura.Situacao)
        {
            case SituacaoFaturaStripe.Paga:
                UltimoPagamentoEm = fatura.PagaEm;
                Status = StatusMensalidade.Ativa;
                break;
            case SituacaoFaturaStripe.Aberta when fatura.Vencimento < hoje:
                Status = StatusMensalidade.Inadimplente;
                break;
        }
    }

    /// <summary>A assinatura acabou no Stripe.</summary>
    public void Cancelar()
    {
        Status = StatusMensalidade.Cancelada;
        LinkPagamento = null;
    }
}

public enum StatusMensalidade
{
    Ativa = 1,
    Inadimplente = 2,
    Cancelada = 3
}

/// <summary>Fatura da mensalidade, como o Stripe a mostra.</summary>
public sealed record FaturaPlataforma(
    string IdInvoice,
    string? IdSubscription,
    decimal Valor,
    DateOnly Vencimento,
    SituacaoFaturaStripe Situacao,
    string? LinkPagamento,
    DateTime? PagaEm);

public static class PlataformaErros
{
    public static readonly Error ValorInvalido =
        Error.Validacao("Plataforma.ValorInvalido", "O valor da mensalidade deve ser maior que zero.");

    public static readonly Error TaxaInvalida =
        Error.Validacao("Plataforma.TaxaInvalida", $"A taxa deve estar entre 0% e {ConfiguracaoPlataforma.TaxaMaxima}%.");

    public static readonly Error MensalidadeNaoEncontrada =
        Error.NaoEncontrado("Plataforma.MensalidadeNaoEncontrada", "A mensalidade da plataforma começa quando a conta de pagamentos for liberada.");
}

public interface IMensalidadeRepository
{
    Task<MensalidadePlataforma?> ObterDaFunerariaAsync(CancellationToken cancellationToken = default);

    /// <summary>Sem o filtro de tenant: o evento da conta da plataforma não diz de qual funerária é.</summary>
    Task<MensalidadePlataforma?> ObterPorSubscriptionAsync(string idSubscription, CancellationToken cancellationToken = default);

    /// <summary>Todas as funerárias, para o ADMIN e para a rotina diária.</summary>
    Task<IReadOnlyList<MensalidadePlataforma>> ListarTodasAsync(CancellationToken cancellationToken = default);

    void Adicionar(MensalidadePlataforma mensalidade);

    Task<ConfiguracaoPlataforma?> ObterConfiguracaoAsync(CancellationToken cancellationToken = default);

    void AdicionarConfiguracao(ConfiguracaoPlataforma configuracao);
}
