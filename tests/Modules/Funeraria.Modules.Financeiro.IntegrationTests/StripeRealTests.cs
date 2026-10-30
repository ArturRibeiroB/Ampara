using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Funeraria.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stripe;

namespace Funeraria.Modules.Financeiro.IntegrationTests;

/// <summary>API com o gateway real. Segundo host no processo: sem servidor do Hangfire (ver DemonstracaoTests).</summary>
public sealed class StripeRealApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public ValueTask InitializeAsync() => IniciarAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Stripe:SecretKey", StripeRealTests.Chave ?? string.Empty);
        builder.UseSetting("Jobs:ServidorHabilitado", "false");
    }
}

[CollectionDefinition(Nome)]
public sealed class StripeRealCollection : ICollectionFixture<StripeRealApiFactory>
{
    public const string Nome = "StripeReal";
}

/// <summary>
/// COB-05 contra o modo de teste do Stripe. Só roda com STRIPE_TEST_SECRET_KEY no ambiente (chave sk_test): sem ela, é pulado.
/// O que cria é apagado no fim.
/// </summary>
[Collection(StripeRealCollection.Nome)]
public sealed class StripeRealTests(StripeRealApiFactory factory)
{
    public static readonly string? Chave = Environment.GetEnvironmentVariable("STRIPE_TEST_SECRET_KEY");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Conta_conectada_e_link_de_cadastro_funcionam_no_stripe()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Chave), "Sem STRIPE_TEST_SECRET_KEY.");
        Assert.StartsWith("sk_test_", Chave!, StringComparison.Ordinal);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IPagamentoGateway>();
        string idConta = await gateway.CriarContaConectadaAsync(Guid.NewGuid(), "Funerária Teste Automatizado", Ct);
        try
        {
            SituacaoConta situacao = await gateway.ObterSituacaoContaAsync(idConta, Ct);
            Assert.False(situacao.CadastroEnviado);

            string link = await gateway.CriarLinkCadastroAsync(idConta, "https://exemplo.test/pronto", "https://exemplo.test/renovar", Ct);
            Assert.StartsWith("https://connect.stripe.com/", link, StringComparison.Ordinal);
        }
        finally
        {
            await new AccountService(new StripeClient(Chave)).DeleteAsync(idConta, cancellationToken: Ct);
        }
    }

    [Fact]
    public async Task Assinatura_emite_a_primeira_fatura_e_baixa_e_dependentes_funcionam_no_stripe()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(Chave), "Sem STRIPE_TEST_SECRET_KEY.");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        var gateway = scope.ServiceProvider.GetRequiredService<IPagamentoGateway>();
        string idConta = await gateway.CriarContaConectadaAsync(Guid.NewGuid(), "Funerária Teste Automatizado", Ct);
        try
        {
            string customer = await gateway.CriarClienteAsync(idConta, Guid.NewGuid(), "Cliente Teste", "cliente@exemplo.test", Ct);
            var contrato = Guid.NewGuid();
            DateOnly hoje = DateOnly.FromDateTime(DateTime.Now);
            AssinaturaCriada assinatura = await gateway.CriarAssinaturaAsync(
                idConta,
                customer,
                new NovaAssinatura(
                    contrato, Guid.NewGuid(), "Plano Teste", PeriodicidadeCobranca.Mensal,
                    CicloCobranca.PrimeiraAncora(hoje, 10, PeriodicidadeCobranca.Mensal), 89.90m, 19.90m, 1),
                Ct);

            // A primeira cobrança sai na hora, com o valor cheio do plano e do dependente, e uma página de pagamento.
            FaturaGateway primeira = assinatura.PrimeiraFatura;
            Assert.Equal(109.80m, primeira.Valor);
            Assert.Equal(SituacaoFaturaStripe.Aberta, primeira.Situacao);
            Assert.StartsWith("https://", primeira.LinkPagamento, StringComparison.Ordinal);
            Assert.Equal(contrato, primeira.IdContrato);
            Assert.Contains(await gateway.ListarFaturasAsync(idConta, customer, assinatura.IdSubscription, contrato, Ct), f => f.IdInvoice == primeira.IdInvoice);

            await gateway.AlterarDependentesAsync(idConta, assinatura.IdSubscription, assinatura.IdItemDependentes, 2, Ct);
            await gateway.ReajustarAsync(
                idConta,
                new ReajusteAssinatura(
                    contrato, Guid.NewGuid(), "Plano Teste", PeriodicidadeCobranca.Mensal, assinatura.IdSubscription, assinatura.IdItemPlano,
                    assinatura.IdItemDependentes, 2, 94.40m, 20.90m, hoje.Year),
                Ct);

            await gateway.MarcarPagaForaAsync(idConta, primeira.IdInvoice, Ct);
            FaturaGateway paga = (await gateway.ObterFaturaAsync(idConta, primeira.IdInvoice, Ct))!;
            Assert.Equal(SituacaoFaturaStripe.Paga, paga.Situacao);

            FaturaGateway avulsa = await gateway.CriarFaturaAvulsaAsync(
                idConta, new CobrancaExtra(contrato, customer, "Multa e juros", 2.10m, $"teste-{Guid.NewGuid():N}"), Ct);
            Assert.Equal(2.10m, avulsa.Valor);
            Assert.Equal(contrato, avulsa.IdContrato);
            Assert.Null(avulsa.IdSubscription);

            await gateway.AdicionarNaProximaFaturaAsync(
                idConta, new CobrancaExtra(contrato, customer, "Multa e juros", 2.10m, $"teste-{Guid.NewGuid():N}"), assinatura.IdSubscription, Ct);
            await gateway.CancelarFaturaAsync(idConta, avulsa.IdInvoice, Ct);
            await gateway.CancelarAssinaturaAsync(idConta, assinatura.IdSubscription, Ct);
        }
        finally
        {
            await new AccountService(new StripeClient(Chave)).DeleteAsync(idConta, cancellationToken: Ct);
        }
    }
}
