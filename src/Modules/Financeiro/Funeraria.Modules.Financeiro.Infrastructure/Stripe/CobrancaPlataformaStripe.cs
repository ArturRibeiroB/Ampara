using System.Collections.Concurrent;
using Funeraria.Modules.Financeiro.Application.Plataforma;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Funeraria.Modules.Financeiro.Domain.Plataforma;
using Stripe;

namespace Funeraria.Modules.Financeiro.Infrastructure.Stripe;

/// <summary>
/// Mensalidade da plataforma no Stripe Billing da conta da própria plataforma (DEC-09): sem <c>Stripe-Account</c>, com a
/// funerária como Customer. Fatura enviada por e-mail, paga por cartão ou boleto na página hospedada pelo Stripe.
/// </summary>
internal sealed class CobrancaPlataformaStripe(IStripeClient cliente) : ICobrancaPlataformaGateway
{
    private const string Moeda = "brl";
    private const string Produto = "mensalidade_plataforma";
    private const int DiasParaPagar = 7;

    public async Task<string> CriarClienteAsync(Guid idTenant, string nome, string email, CancellationToken cancellationToken)
    {
        Customer customer = await new CustomerService(cliente).CreateAsync(
            new CustomerCreateOptions
            {
                Name = nome,
                Email = email,
                PreferredLocales = ["pt-BR"],
                Metadata = new Dictionary<string, string> { ["id_tenant"] = idTenant.ToString() }
            },
            new RequestOptions { IdempotencyKey = $"plataforma-cliente-{idTenant}" },
            cancellationToken);
        return customer.Id;
    }

    public async Task<string> CriarAssinaturaAsync(Guid idTenant, string idCustomer, decimal valorMensal, CancellationToken cancellationToken)
    {
        await GarantirProdutoAsync(cancellationToken);
        Subscription assinatura = await new SubscriptionService(cliente).CreateAsync(
            new SubscriptionCreateOptions
            {
                Customer = idCustomer,
                Items = [Item(valorMensal)],
                CollectionMethod = "send_invoice",
                DaysUntilDue = DiasParaPagar,
                PaymentSettings = new SubscriptionPaymentSettingsOptions { PaymentMethodTypes = ["card", "boleto"] },
                Metadata = new Dictionary<string, string> { ["id_tenant"] = idTenant.ToString() }
            },
            new RequestOptions { IdempotencyKey = $"plataforma-assinatura-{idTenant}" },
            cancellationToken);
        return assinatura.Id;
    }

    public async Task AlterarValorAsync(Guid idTenant, string idSubscription, decimal valorMensal, CancellationToken cancellationToken)
    {
        var assinaturas = new SubscriptionService(cliente);
        Subscription atual = await assinaturas.GetAsync(idSubscription, cancellationToken: cancellationToken);
        SubscriptionItemOptions item = Item(valorMensal);
        item.Id = atual.Items.Data[0].Id;
        await assinaturas.UpdateAsync(
            idSubscription,
            new SubscriptionUpdateOptions { Items = [item], ProrationBehavior = "none" },
            cancellationToken: cancellationToken);
    }

    public async Task<FaturaPlataforma?> ObterFaturaAsync(string idInvoice, CancellationToken cancellationToken)
    {
        Invoice i = await new InvoiceService(cliente).GetAsync(idInvoice, cancellationToken: cancellationToken);
        if (i.Status == "draft")
        {
            return null;
        }

        SituacaoFaturaStripe situacao = i.Status switch
        {
            "paid" => SituacaoFaturaStripe.Paga,
            "void" or "uncollectible" => SituacaoFaturaStripe.Cancelada,
            _ => SituacaoFaturaStripe.Aberta
        };
        return new FaturaPlataforma(
            i.Id,
            i.Parent?.SubscriptionDetails?.SubscriptionId,
            i.AmountDue / 100m,
            DateOnly.FromDateTime(i.DueDate ?? i.Created),
            situacao,
            i.HostedInvoiceUrl,
            i.StatusTransitions?.PaidAt);
    }

    private static SubscriptionItemOptions Item(decimal valorMensal) =>
        new()
        {
            Quantity = 1,
            PriceData = new SubscriptionItemPriceDataOptions
            {
                Currency = Moeda,
                Product = Produto,
                UnitAmount = (long)decimal.Round(valorMensal * 100m, 0, MidpointRounding.AwayFromZero),
                Recurring = new SubscriptionItemPriceDataRecurringOptions { Interval = "month" }
            }
        };

    /// <summary>Produto com id fixo na conta da plataforma: criar de novo é inofensivo.</summary>
    private async Task GarantirProdutoAsync(CancellationToken cancellationToken)
    {
        try
        {
            await new ProductService(cliente).CreateAsync(
                new ProductCreateOptions { Id = Produto, Name = "Mensalidade da plataforma" }, cancellationToken: cancellationToken);
        }
        catch (StripeException e) when (e.StripeError?.Code == "resource_already_exists")
        {
            // Já existe: é o caso comum a partir da segunda funerária.
        }
    }
}

/// <summary>
/// Mensalidade da plataforma em memória, para testes e desenvolvimento sem chave. O teste simula as faturas que viriam do
/// Stripe e confere as chamadas feitas.
/// </summary>
public sealed class CobrancaPlataformaFake : ICobrancaPlataformaGateway
{
    private readonly ConcurrentDictionary<string, FaturaPlataforma> _faturas = new();
    private readonly ConcurrentDictionary<Guid, string> _assinaturas = new();
    private readonly ConcurrentQueue<string> _chamadas = new();
    private int _sequencia;

    /// <summary>Ex.: "assinatura:sub_plataforma_...:149", "valor:sub_plataforma_...:199".</summary>
    public IReadOnlyCollection<string> Chamadas => _chamadas;

    /// <summary>Assinatura criada para a funerária, ou nula.</summary>
    public string? AssinaturaDa(Guid idTenant) => _assinaturas.GetValueOrDefault(idTenant);

    public FaturaPlataforma EmitirFatura(string idSubscription, DateOnly vencimento, decimal valor)
    {
        var fatura = new FaturaPlataforma(
            $"in_plataforma_{Interlocked.Increment(ref _sequencia)}", idSubscription, valor, vencimento, SituacaoFaturaStripe.Aberta,
            "https://pagar.stripe.test/mensalidade", null);
        _faturas[fatura.IdInvoice] = fatura;
        return fatura;
    }

    public FaturaPlataforma Pagar(string idInvoice, DateTime em)
    {
        FaturaPlataforma paga = _faturas[idInvoice] with { Situacao = SituacaoFaturaStripe.Paga, PagaEm = em };
        _faturas[idInvoice] = paga;
        return paga;
    }

    public Task<string> CriarClienteAsync(Guid idTenant, string nome, string email, CancellationToken cancellationToken) =>
        Task.FromResult($"cus_plataforma_{idTenant:N}");

    public Task<string> CriarAssinaturaAsync(Guid idTenant, string idCustomer, decimal valorMensal, CancellationToken cancellationToken)
    {
        string id = $"sub_plataforma_{idTenant:N}";
        _assinaturas[idTenant] = id;
        _chamadas.Enqueue($"assinatura:{id}:{valorMensal}");
        return Task.FromResult(id);
    }

    public Task AlterarValorAsync(Guid idTenant, string idSubscription, decimal valorMensal, CancellationToken cancellationToken)
    {
        _chamadas.Enqueue($"valor:{idSubscription}:{valorMensal}");
        return Task.CompletedTask;
    }

    public Task<FaturaPlataforma?> ObterFaturaAsync(string idInvoice, CancellationToken cancellationToken) =>
        Task.FromResult(_faturas.GetValueOrDefault(idInvoice));
}
