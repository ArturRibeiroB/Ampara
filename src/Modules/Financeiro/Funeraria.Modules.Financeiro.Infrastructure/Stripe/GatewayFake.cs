using System.Collections.Concurrent;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Domain.Cobranca;

namespace Funeraria.Modules.Financeiro.Infrastructure.Stripe;

/// <summary>
/// Stripe em memória para testes e desenvolvimento sem chave (COB-05). Emite a primeira fatura ao criar a assinatura e deixa o
/// teste simular o que viria do Stripe: conta liberada, novas faturas e pagamentos.
/// </summary>
public sealed class GatewayFake : IPagamentoGateway
{
    private readonly ConcurrentDictionary<string, SituacaoConta> _contas = new();
    private readonly ConcurrentDictionary<string, FaturaGateway> _faturas = new();
    private readonly ConcurrentDictionary<string, NovaAssinatura> _assinaturas = new();
    private readonly ConcurrentQueue<string> _chamadas = new();
    private int _sequencia;

    /// <summary>Operações feitas, para o teste conferir. Ex.: "dependentes:sub_1:2", "cancelar:sub_1".</summary>
    public IReadOnlyCollection<string> Chamadas => _chamadas;

    public IReadOnlyCollection<CobrancaExtra> Extras => _extras;

    private readonly ConcurrentQueue<CobrancaExtra> _extras = new();

    public void LiberarConta(string idConta) => _contas[idConta] = new SituacaoConta(true, true);

    /// <summary>Simula uma fatura nova do Stripe (renovação), com o vencimento informado.</summary>
    public FaturaGateway EmitirFatura(string idSubscription, DateOnly vencimento, decimal valor)
    {
        NovaAssinatura assinatura = _assinaturas[idSubscription];
        var fatura = new FaturaGateway(
            $"in_{Interlocked.Increment(ref _sequencia)}", idSubscription, assinatura.IdContrato, vencimento.AddDays(-5), vencimento.AddMonths(1),
            valor, vencimento, SituacaoFaturaStripe.Aberta, "https://pagar.stripe.test/fatura", null, null);
        _faturas[fatura.IdInvoice] = fatura;
        return fatura;
    }

    public FaturaGateway Pagar(string idInvoice, DateTime em, FormaPagamento forma = FormaPagamento.Cartao)
    {
        FaturaGateway paga = _faturas[idInvoice] with { Situacao = SituacaoFaturaStripe.Paga, PagaEm = em, FormaPagamento = forma };
        _faturas[idInvoice] = paga;
        return paga;
    }

    public FaturaGateway? Fatura(string idInvoice) => _faturas.GetValueOrDefault(idInvoice);

    public Task<string> CriarContaConectadaAsync(Guid idTenant, string nomeFantasia, CancellationToken cancellationToken)
    {
        string id = $"acct_{idTenant:N}";
        _contas.TryAdd(id, new SituacaoConta(false, false));
        return Task.FromResult(id);
    }

    public Task<SituacaoConta> ObterSituacaoContaAsync(string idConta, CancellationToken cancellationToken) =>
        Task.FromResult(_contas.GetValueOrDefault(idConta, new SituacaoConta(false, false)));

    public Task<string> CriarLinkCadastroAsync(string idConta, string urlRetorno, string urlRenovar, CancellationToken cancellationToken) =>
        Task.FromResult($"https://connect.stripe.test/cadastro/{idConta}");

    public Task<string> CriarClienteAsync(string idConta, Guid idCliente, string nome, string email, CancellationToken cancellationToken) =>
        Task.FromResult($"cus_{idCliente:N}");

    public Task<AssinaturaCriada> CriarAssinaturaAsync(string idConta, string idCustomer, NovaAssinatura a, CancellationToken cancellationToken)
    {
        string id = $"sub_{a.IdContrato:N}";
        _assinaturas[id] = a;
        var primeira = new FaturaGateway(
            $"in_{Interlocked.Increment(ref _sequencia)}", null, a.IdContrato, DateOnly.FromDateTime(DateTime.Now), a.Ancora,
            a.ValorPlano + (a.ValorPorDependente * a.QuantidadeDependentes), DateOnly.FromDateTime(DateTime.Now).AddDays(CicloCobranca.DiasDeAntecedencia),
            SituacaoFaturaStripe.Aberta, "https://pagar.stripe.test/fatura", null, null);
        _faturas[primeira.IdInvoice] = primeira;
        return Task.FromResult(new AssinaturaCriada(id, $"si_plano_{a.IdContrato:N}", $"si_dep_{a.IdContrato:N}", primeira));
    }

    public Task AlterarDependentesAsync(string idConta, string idSubscription, string idItem, int quantidade, CancellationToken cancellationToken)
    {
        _chamadas.Enqueue($"dependentes:{idSubscription}:{quantidade}");
        return Task.CompletedTask;
    }

    public Task ReajustarAsync(string idConta, ReajusteAssinatura reajuste, CancellationToken cancellationToken)
    {
        _chamadas.Enqueue($"reajuste:{reajuste.IdSubscription}:{reajuste.ValorPlano}:{reajuste.ValorPorDependente}");
        return Task.CompletedTask;
    }

    public Task CancelarAssinaturaAsync(string idConta, string idSubscription, CancellationToken cancellationToken)
    {
        _chamadas.Enqueue($"cancelar:{idSubscription}");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FaturaGateway>> ListarFaturasAsync(
        string idConta, string idCustomer, string idSubscription, Guid idContrato, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FaturaGateway>>([.. _faturas.Values.Where(f => f.IdSubscription == idSubscription || f.IdContrato == idContrato)]);

    public Task<FaturaGateway?> ObterFaturaAsync(string idConta, string idInvoice, CancellationToken cancellationToken) =>
        Task.FromResult(_faturas.GetValueOrDefault(idInvoice));

    public Task<string?> ObterFaturaDoPagamentoAsync(string idConta, string idPagamento, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(idPagamento.Replace("pi_de_", string.Empty, StringComparison.Ordinal));

    public Task MarcarPagaForaAsync(string idConta, string idInvoice, CancellationToken cancellationToken)
    {
        Pagar(idInvoice, DateTime.UtcNow, FormaPagamento.Manual);
        _chamadas.Enqueue($"paga-fora:{idInvoice}");
        return Task.CompletedTask;
    }

    public Task CancelarFaturaAsync(string idConta, string idInvoice, CancellationToken cancellationToken)
    {
        _faturas[idInvoice] = _faturas[idInvoice] with { Situacao = SituacaoFaturaStripe.Cancelada };
        return Task.CompletedTask;
    }

    public Task AdicionarNaProximaFaturaAsync(string idConta, CobrancaExtra cobranca, string idSubscription, CancellationToken cancellationToken)
    {
        if (!_extras.Any(e => e.ChaveIdempotencia == cobranca.ChaveIdempotencia))
        {
            _extras.Enqueue(cobranca);
        }

        return Task.CompletedTask;
    }

    public Task<FaturaGateway> CriarFaturaAvulsaAsync(string idConta, CobrancaExtra cobranca, CancellationToken cancellationToken)
    {
        _extras.Enqueue(cobranca);
        var fatura = new FaturaGateway(
            $"in_{Interlocked.Increment(ref _sequencia)}", null, cobranca.IdContrato, DateOnly.FromDateTime(DateTime.Now), null, cobranca.Valor,
            DateOnly.FromDateTime(DateTime.Now).AddDays(CicloCobranca.DiasDeAntecedencia), SituacaoFaturaStripe.Aberta, "https://pagar.stripe.test/avulsa", null, null);
        _faturas[fatura.IdInvoice] = fatura;
        return Task.FromResult(fatura);
    }
}
