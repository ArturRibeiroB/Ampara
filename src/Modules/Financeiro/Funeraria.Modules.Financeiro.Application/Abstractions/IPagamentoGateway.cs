using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.Modules.Financeiro.Domain.Cobranca;

namespace Funeraria.Modules.Financeiro.Application.Abstractions;

public interface IFinanceiroUnitOfWork : IUnitOfWork;

/// <summary>
/// Pagamentos (COB-05). A implementação real é o Stripe, sempre na conta conectada da funerária (DEC-09); os testes usam uma
/// fake. Dados de cartão nunca passam por aqui: o cliente paga na página hospedada pelo Stripe.
/// </summary>
public interface IPagamentoGateway
{
    Task<string> CriarContaConectadaAsync(Guid idTenant, string nomeFantasia, CancellationToken cancellationToken);

    Task<SituacaoConta> ObterSituacaoContaAsync(string idConta, CancellationToken cancellationToken);

    Task<string> CriarLinkCadastroAsync(string idConta, string urlRetorno, string urlRenovar, CancellationToken cancellationToken);

    Task<string> CriarClienteAsync(string idConta, Guid idCliente, string nome, string email, CancellationToken cancellationToken);

    Task<AssinaturaCriada> CriarAssinaturaAsync(string idConta, string idCustomer, NovaAssinatura assinatura, CancellationToken cancellationToken);

    /// <summary>Vale na próxima fatura, sem cobrança proporcional (CON-02).</summary>
    Task AlterarDependentesAsync(string idConta, string idSubscription, string idItem, int quantidade, CancellationToken cancellationToken);

    /// <summary>Troca os preços da assinatura, sem cobrança proporcional (COB-08).</summary>
    Task ReajustarAsync(string idConta, ReajusteAssinatura reajuste, CancellationToken cancellationToken);

    Task CancelarAssinaturaAsync(string idConta, string idSubscription, CancellationToken cancellationToken);

    /// <summary>Faturas do contrato: as de ciclo da assinatura e as avulsas ligadas a ele, como a primeira cobrança.</summary>
    Task<IReadOnlyList<FaturaGateway>> ListarFaturasAsync(
        string idConta, string idCustomer, string idSubscription, Guid idContrato, CancellationToken cancellationToken);

    Task<FaturaGateway?> ObterFaturaAsync(string idConta, string idInvoice, CancellationToken cancellationToken);

    /// <summary>Fatura de um pagamento (boleto ou Pix em compensação). Nula se o pagamento não for de uma fatura.</summary>
    Task<string?> ObterFaturaDoPagamentoAsync(string idConta, string idPagamento, CancellationToken cancellationToken);

    /// <summary>Marca como paga fora do Stripe, para ele parar de cobrar (COB-07).</summary>
    Task MarcarPagaForaAsync(string idConta, string idInvoice, CancellationToken cancellationToken);

    Task CancelarFaturaAsync(string idConta, string idInvoice, CancellationToken cancellationToken);

    /// <summary>Lança um valor na próxima fatura da assinatura (multa e juros do plano mensal, COB-09).</summary>
    Task AdicionarNaProximaFaturaAsync(string idConta, CobrancaExtra cobranca, string idSubscription, CancellationToken cancellationToken);

    /// <summary>Emite uma fatura separada (multa e juros do plano anual, COB-09; adicionais do funeral, FUN-04).</summary>
    Task<FaturaGateway> CriarFaturaAvulsaAsync(string idConta, CobrancaExtra cobranca, CancellationToken cancellationToken);
}

/// <summary>Lê e valida o corpo do webhook (COB-06).</summary>
public interface ILeitorWebhook
{
    /// <summary>Nulo quando a assinatura do cabeçalho não confere.</summary>
    EventoGateway? Validar(string corpo, string assinatura);

    /// <summary>Lê de novo um corpo já validado e gravado, no processamento em segundo plano.</summary>
    EventoGateway Ler(string corpo);
}

public sealed record SituacaoConta(bool CobrancasHabilitadas, bool CadastroEnviado);

public sealed record NovaAssinatura(
    Guid IdContrato,
    Guid IdPlano,
    string NomePlano,
    PeriodicidadeCobranca Periodicidade,
    DateOnly Ancora,
    decimal ValorPlano,
    decimal ValorPorDependente,
    int QuantidadeDependentes);

/// <param name="IdSubscription">Assinatura.</param>
/// <param name="IdItemPlano">Item do plano.</param>
/// <param name="IdItemDependentes">Item dos dependentes.</param>
/// <param name="PrimeiraFatura">
/// Primeira cobrança, já emitida. É uma fatura própria: com a âncora no futuro, o Stripe só gera a fatura de ciclo na âncora
/// (conferido no modo de teste), e o contrato ficaria semanas sem nada a pagar.
/// </param>
public sealed record AssinaturaCriada(string IdSubscription, string IdItemPlano, string IdItemDependentes, FaturaGateway PrimeiraFatura);

public sealed record ReajusteAssinatura(
    Guid IdContrato,
    Guid IdPlano,
    string NomePlano,
    PeriodicidadeCobranca Periodicidade,
    string IdSubscription,
    string IdItemPlano,
    string IdItemDependentes,
    int QuantidadeDependentes,
    decimal ValorPlano,
    decimal ValorPorDependente,
    int Ano);

/// <summary>Valor fora da recorrência. A chave garante que o mesmo lançamento não é cobrado duas vezes.</summary>
/// <param name="IdContrato">Contrato.</param>
/// <param name="IdCustomer">Cliente no Stripe.</param>
/// <param name="Descricao">O que aparece na fatura.</param>
/// <param name="Valor">Valor.</param>
/// <param name="ChaveIdempotencia">Chave do lançamento.</param>
/// <param name="ForaDaInadimplencia">Não pagar não suspende o plano (itens do funeral, FUN-04).</param>
public sealed record CobrancaExtra(
    Guid IdContrato, string IdCustomer, string Descricao, decimal Valor, string ChaveIdempotencia, bool ForaDaInadimplencia = false);

public sealed record FaturaGateway(
    string IdInvoice,
    string? IdSubscription,
    Guid? IdContrato,
    DateOnly Competencia,
    DateOnly? FimDoPeriodo,
    decimal Valor,
    DateOnly DataVencimento,
    SituacaoFaturaStripe Situacao,
    string? LinkPagamento,
    DateTime? PagaEm,
    FormaPagamento? FormaPagamento,
    bool ForaDaInadimplencia = false);

/// <summary>Evento recebido. Tem só o que o sistema usa: a API de fatura é consultada de novo no processamento.</summary>
public sealed record EventoGateway(string Id, string Tipo, string? IdConta, string? IdObjeto, string? IdSubscription);

public static class TiposEventoStripe
{
    public const string FaturaCriada = "invoice.created";
    public const string FaturaFinalizada = "invoice.finalized";
    public const string FaturaAtualizada = "invoice.updated";
    public const string FaturaPaga = "invoice.paid";
    public const string FaturaFalhou = "invoice.payment_failed";
    public const string FaturaCancelada = "invoice.voided";
    public const string AssinaturaAtualizada = "customer.subscription.updated";
    public const string AssinaturaRemovida = "customer.subscription.deleted";
    public const string ContaAtualizada = "account.updated";
    public const string PagamentoProcessando = "payment_intent.processing";
    public const string PagamentoFalhou = "payment_intent.payment_failed";
}
