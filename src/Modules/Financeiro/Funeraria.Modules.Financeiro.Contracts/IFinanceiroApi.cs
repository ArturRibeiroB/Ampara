namespace Funeraria.Modules.Financeiro.Contracts;

/// <summary>Cobrança pedida por outros módulos.</summary>
public interface IFinanceiroApi
{
    /// <summary>
    /// Fatura separada dos itens do funeral (FUN-04), que não suspende o plano nem gera multa e juros. A chave evita cobrar duas
    /// vezes numa nova tentativa.
    /// </summary>
    /// <returns>Nula se a funerária ainda não recebe pelo Stripe ou o contrato não tem cobrança.</returns>
    Task<FaturaAvulsaDto?> EmitirFaturaDoFuneralAsync(
        Guid idContrato, string descricao, decimal valor, string chaveIdempotencia, CancellationToken cancellationToken);

    /// <summary>Cancela a fatura se ela ainda estiver em aberto. Paga ou em compensação, fica como está.</summary>
    Task CancelarFaturaEmAbertoAsync(Guid idFatura, CancellationToken cancellationToken);
}

public sealed record FaturaAvulsaDto(Guid IdFatura, decimal Valor, DateOnly DataVencimento, string? LinkPagamento);
