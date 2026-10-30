using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Contracts;
using Funeraria.Modules.Financeiro.Domain.Cobranca;

namespace Funeraria.Modules.Financeiro.Application.Cobrancas;

public sealed class FinanceiroApi(
    IContaPagamentoRepository contas,
    IAssinaturaRepository assinaturas,
    IFaturaRepository faturas,
    IPagamentoGateway gateway,
    EspelhoDeFaturas espelho)
    : IFinanceiroApi
{
    public async Task<FaturaAvulsaDto?> EmitirFaturaDoFuneralAsync(
        Guid idContrato, string descricao, decimal valor, string chaveIdempotencia, CancellationToken cancellationToken)
    {
        if (await contas.ObterDaFunerariaAsync(cancellationToken) is not { CobrancasHabilitadas: true } conta
            || await assinaturas.ObterDoContratoAsync(idContrato, cancellationToken) is not Assinatura assinatura
            || (assinatura.IdStripeCustomer ?? await assinaturas.ObterCustomerAsync(assinatura.IdCliente, cancellationToken)) is not string idCustomer)
        {
            return null;
        }

        FaturaGateway emitida = await gateway.CriarFaturaAvulsaAsync(
            conta.IdStripeAccount,
            new CobrancaExtra(idContrato, idCustomer, descricao, valor, chaveIdempotencia, ForaDaInadimplencia: true),
            cancellationToken);

        // Não depende do webhook para aparecer ao cliente.
        Fatura? fatura = await espelho.EspelharAsync(emitida, assinatura, cancellationToken);
        return fatura is null ? null : new FaturaAvulsaDto(fatura.Id, fatura.Valor, fatura.DataVencimento, fatura.LinkPagamento);
    }

    public async Task CancelarFaturaEmAbertoAsync(Guid idFatura, CancellationToken cancellationToken)
    {
        if (await faturas.ObterAsync(idFatura, cancellationToken) is not { Status: StatusFatura.Pendente or StatusFatura.Vencida } fatura
            || await contas.ObterDaFunerariaAsync(cancellationToken) is not ContaPagamento conta)
        {
            return;
        }

        await gateway.CancelarFaturaAsync(conta.IdStripeAccount, fatura.IdStripeInvoice, cancellationToken);
        if (await gateway.ObterFaturaAsync(conta.IdStripeAccount, fatura.IdStripeInvoice, cancellationToken) is FaturaGateway atual)
        {
            await espelho.EspelharAsync(atual, null, cancellationToken);
        }
    }
}
