using System.Text.Json;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Microsoft.Extensions.Options;
using Stripe;

namespace Funeraria.Modules.Financeiro.Infrastructure.Stripe;

/// <summary>Seção "Stripe" da configuração. As chaves ficam em secrets, nunca no código nem no appsettings (COB-05).</summary>
public sealed class StripeOptions
{
    public const string Section = "Stripe";

    public string? SecretKey { get; set; }

    /// <summary>Segredo do endpoint do webhook (whsec_...), para validar o cabeçalho Stripe-Signature (COB-06).</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>Sem chave, usa o gateway fake. Só em desenvolvimento e testes: em produção, a falta da chave é erro.</summary>
    public bool PermitirFake { get; set; }

    /// <summary>Taxa da plataforma retida em cada pagamento (DEC-09).</summary>
    public decimal TaxaPlataformaPercentual { get; set; } = 2;

    /// <summary>
    /// Aceita Pix nas faturas fora do ciclo (DEC-09). Só ligar com o Pix ativo nas contas: pedir um meio indisponível faz o
    /// Stripe recusar a finalização da fatura (conferido no modo de teste).
    /// </summary>
    public bool PixHabilitado { get; set; }
}

/// <summary>
/// Stripe Billing com Connect (COB-05, DEC-09): tudo na conta conectada da funerária, com a taxa da plataforma na assinatura.
/// A retentativa é a embutida no Stripe.net, e toda criação leva chave de idempotência.
/// </summary>
internal sealed class StripeGateway(IStripeClient cliente, IOptions<StripeOptions> opcoes) : IPagamentoGateway
{
    private const string Moeda = "brl";

    public async Task<string> CriarContaConectadaAsync(Guid idTenant, string nomeFantasia, CancellationToken cancellationToken)
    {
        Account conta = await new AccountService(cliente).CreateAsync(
            new AccountCreateOptions
            {
                Type = "standard",
                Country = "BR",
                BusinessProfile = new AccountBusinessProfileOptions { Name = nomeFantasia },
                Metadata = new Dictionary<string, string> { ["id_tenant"] = idTenant.ToString() }
            },
            new RequestOptions { IdempotencyKey = $"conta-{idTenant}" },
            cancellationToken);
        return conta.Id;
    }

    public async Task<SituacaoConta> ObterSituacaoContaAsync(string idConta, CancellationToken cancellationToken)
    {
        Account conta = await new AccountService(cliente).GetAsync(idConta, cancellationToken: cancellationToken);
        return new SituacaoConta(conta.ChargesEnabled, conta.DetailsSubmitted);
    }

    public async Task<string> CriarLinkCadastroAsync(string idConta, string urlRetorno, string urlRenovar, CancellationToken cancellationToken)
    {
        AccountLink link = await new AccountLinkService(cliente).CreateAsync(
            new AccountLinkCreateOptions { Account = idConta, Type = "account_onboarding", ReturnUrl = urlRetorno, RefreshUrl = urlRenovar },
            cancellationToken: cancellationToken);
        return link.Url;
    }

    public async Task<string> CriarClienteAsync(string idConta, Guid idCliente, string nome, string email, CancellationToken cancellationToken)
    {
        Customer customer = await new CustomerService(cliente).CreateAsync(
            new CustomerCreateOptions
            {
                Name = nome,
                Email = email,
                Metadata = new Dictionary<string, string> { ["id_cliente"] = idCliente.ToString() }
            },
            Opcoes(idConta, $"cliente-{idCliente}"),
            cancellationToken);
        return customer.Id;
    }

    public async Task<AssinaturaCriada> CriarAssinaturaAsync(string idConta, string idCustomer, NovaAssinatura a, CancellationToken cancellationToken)
    {
        string produto = await GarantirProdutoAsync(idConta, a.IdPlano, a.NomePlano, cancellationToken);
        string intervalo = Intervalo(a.Periodicidade);
        var itens = new List<SubscriptionItemOptions>
        {
            Item(produto, a.ValorPlano, 1, intervalo, "plano"),
            Item(produto, a.ValorPorDependente, a.QuantidadeDependentes, intervalo, "dependentes")
        };

        Subscription assinatura = await new SubscriptionService(cliente).CreateAsync(
            new SubscriptionCreateOptions
            {
                Customer = idCustomer,
                Items = itens,
                BillingCycleAnchor = a.Ancora.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
                ProrationBehavior = "none",
                CollectionMethod = "send_invoice",
                DaysUntilDue = CicloCobranca.DiasDeAntecedencia,
                ApplicationFeePercent = opcoes.Value.TaxaPlataformaPercentual,
                PaymentSettings = new SubscriptionPaymentSettingsOptions { PaymentMethodTypes = ["card", "boleto"] },
                Metadata = new Dictionary<string, string> { ["id_contrato"] = a.IdContrato.ToString() }
            },
            Opcoes(idConta, $"assinatura-{a.IdContrato}"),
            cancellationToken);

        // A primeira cobrança sai na contratação e cobre o ciclo até a âncora; o Stripe só faz faturas de ciclo a partir dela.
        FaturaGateway primeira = await CriarFaturaAvulsaAsync(
            idConta,
            new CobrancaExtra(
                a.IdContrato, idCustomer, $"Primeira cobrança: {a.NomePlano}", a.ValorPlano + (a.ValorPorDependente * a.QuantidadeDependentes), $"primeira-{a.IdContrato}"),
            cancellationToken);

        return new AssinaturaCriada(assinatura.Id, IdItem(assinatura, "plano"), IdItem(assinatura, "dependentes"), primeira);
    }

    public Task AlterarDependentesAsync(string idConta, string idSubscription, string idItem, int quantidade, CancellationToken cancellationToken) =>
        new SubscriptionItemService(cliente).UpdateAsync(
            idItem,
            new SubscriptionItemUpdateOptions { Quantity = quantidade, ProrationBehavior = "none" },
            Opcoes(idConta),
            cancellationToken);

    public async Task ReajustarAsync(string idConta, ReajusteAssinatura r, CancellationToken cancellationToken)
    {
        string produto = await GarantirProdutoAsync(idConta, r.IdPlano, r.NomePlano, cancellationToken);
        string intervalo = Intervalo(r.Periodicidade);
        SubscriptionItemOptions plano = Item(produto, r.ValorPlano, 1, intervalo, "plano");
        plano.Id = r.IdItemPlano;
        SubscriptionItemOptions dependentes = Item(produto, r.ValorPorDependente, r.QuantidadeDependentes, intervalo, "dependentes");
        dependentes.Id = r.IdItemDependentes;

        await new SubscriptionService(cliente).UpdateAsync(
            r.IdSubscription,
            new SubscriptionUpdateOptions { Items = [plano, dependentes], ProrationBehavior = "none" },
            Opcoes(idConta, $"reajuste-{r.IdContrato}-{r.Ano}"),
            cancellationToken);
    }

    public Task CancelarAssinaturaAsync(string idConta, string idSubscription, CancellationToken cancellationToken) =>
        new SubscriptionService(cliente).CancelAsync(idSubscription, options: null, Opcoes(idConta), cancellationToken);

    public async Task<IReadOnlyList<FaturaGateway>> ListarFaturasAsync(
        string idConta, string idCustomer, string idSubscription, Guid idContrato, CancellationToken cancellationToken)
    {
        StripeList<Invoice> faturas = await new InvoiceService(cliente).ListAsync(
            new InvoiceListOptions { Customer = idCustomer, Limit = 100 }, Opcoes(idConta), cancellationToken);
        var lista = new List<FaturaGateway>();
        foreach (Invoice invoice in faturas.Data.Where(i => i.Status != "draft"))
        {
            FaturaGateway fatura = await MapearAsync(idConta, invoice, cancellationToken);
            if (fatura.IdSubscription == idSubscription || fatura.IdContrato == idContrato)
            {
                lista.Add(fatura);
            }
        }

        return lista;
    }

    public async Task<FaturaGateway?> ObterFaturaAsync(string idConta, string idInvoice, CancellationToken cancellationToken)
    {
        Invoice invoice = await new InvoiceService(cliente).GetAsync(idInvoice, options: null, Opcoes(idConta), cancellationToken);
        return invoice.Status == "draft" ? null : await MapearAsync(idConta, invoice, cancellationToken);
    }

    public async Task<string?> ObterFaturaDoPagamentoAsync(string idConta, string idPagamento, CancellationToken cancellationToken)
    {
        StripeList<InvoicePayment> pagamentos = await new InvoicePaymentService(cliente).ListAsync(
            new InvoicePaymentListOptions
            {
                Payment = new InvoicePaymentPaymentOptions { Type = "payment_intent", PaymentIntent = idPagamento },
                Limit = 1
            },
            Opcoes(idConta),
            cancellationToken);
        return pagamentos.Data.FirstOrDefault()?.InvoiceId;
    }

    public Task MarcarPagaForaAsync(string idConta, string idInvoice, CancellationToken cancellationToken) =>
        new InvoiceService(cliente).PayAsync(idInvoice, new InvoicePayOptions { PaidOutOfBand = true }, Opcoes(idConta), cancellationToken);

    public Task CancelarFaturaAsync(string idConta, string idInvoice, CancellationToken cancellationToken) =>
        new InvoiceService(cliente).VoidInvoiceAsync(idInvoice, options: null, Opcoes(idConta), cancellationToken);

    public Task AdicionarNaProximaFaturaAsync(string idConta, CobrancaExtra c, string idSubscription, CancellationToken cancellationToken) =>
        new InvoiceItemService(cliente).CreateAsync(
            new InvoiceItemCreateOptions
            {
                Customer = c.IdCustomer,
                Subscription = idSubscription,
                Amount = Centavos(c.Valor),
                Currency = Moeda,
                Description = c.Descricao
            },
            Opcoes(idConta, c.ChaveIdempotencia),
            cancellationToken);

    public async Task<FaturaGateway> CriarFaturaAvulsaAsync(string idConta, CobrancaExtra c, CancellationToken cancellationToken)
    {
        var faturas = new InvoiceService(cliente);
        Invoice invoice = await faturas.CreateAsync(
            new InvoiceCreateOptions
            {
                Customer = c.IdCustomer,
                AutoAdvance = false,
                CollectionMethod = "send_invoice",
                DaysUntilDue = CicloCobranca.DiasDeAntecedencia,
                Description = c.Descricao,
                ApplicationFeeAmount = Centavos(c.Valor * opcoes.Value.TaxaPlataformaPercentual / 100m),
                PaymentSettings = new InvoicePaymentSettingsOptions
                {
                    PaymentMethodTypes = opcoes.Value.PixHabilitado ? ["card", "boleto", "pix"] : ["card", "boleto"]
                },
                Metadata = new Dictionary<string, string> { ["id_contrato"] = c.IdContrato.ToString() }
            },
            Opcoes(idConta, $"{c.ChaveIdempotencia}-fatura"),
            cancellationToken);

        await new InvoiceItemService(cliente).CreateAsync(
            new InvoiceItemCreateOptions { Customer = c.IdCustomer, Invoice = invoice.Id, Amount = Centavos(c.Valor), Currency = Moeda, Description = c.Descricao },
            Opcoes(idConta, $"{c.ChaveIdempotencia}-item"),
            cancellationToken);

        // Numa nova tentativa, a chave de idempotência devolve a mesma fatura, que pode já estar finalizada.
        Invoice atual = await faturas.GetAsync(invoice.Id, options: null, Opcoes(idConta), cancellationToken);
        Invoice finalizada = atual.Status == "draft"
            ? await faturas.FinalizeInvoiceAsync(invoice.Id, options: null, Opcoes(idConta), cancellationToken)
            : atual;
        return await MapearAsync(idConta, finalizada, cancellationToken);
    }

    private static RequestOptions Opcoes(string idConta, string? chaveIdempotencia = null) =>
        new() { StripeAccount = idConta, IdempotencyKey = chaveIdempotencia };

    private static long Centavos(decimal valor) => (long)decimal.Round(valor * 100m, 0, MidpointRounding.AwayFromZero);

    private static string Intervalo(PeriodicidadeCobranca periodicidade) => periodicidade == PeriodicidadeCobranca.Anual ? "year" : "month";

    private static SubscriptionItemOptions Item(string produto, decimal valor, int quantidade, string intervalo, string tipo) =>
        new()
        {
            Quantity = quantidade,
            Metadata = new Dictionary<string, string> { ["tipo"] = tipo },
            PriceData = new SubscriptionItemPriceDataOptions
            {
                Currency = Moeda,
                Product = produto,
                UnitAmount = Centavos(valor),
                Recurring = new SubscriptionItemPriceDataRecurringOptions { Interval = intervalo }
            }
        };

    private static string IdItem(Subscription assinatura, string tipo) =>
        assinatura.Items.Data.First(i => i.Metadata.TryGetValue("tipo", out string? t) && t == tipo).Id;

    /// <summary>O produto do plano tem id fixo na conta: criar de novo é inofensivo, e nada precisa ser guardado aqui.</summary>
    private async Task<string> GarantirProdutoAsync(string idConta, Guid idPlano, string nome, CancellationToken cancellationToken)
    {
        string id = $"plano_{idPlano:N}";
        try
        {
            await new ProductService(cliente).CreateAsync(new ProductCreateOptions { Id = id, Name = nome }, Opcoes(idConta), cancellationToken);
        }
        catch (StripeException e) when (e.StripeError?.Code == "resource_already_exists")
        {
            // Já existe: é o caso comum a partir do segundo contrato do plano.
        }

        return id;
    }

    private async Task<FaturaGateway> MapearAsync(string idConta, Invoice i, CancellationToken cancellationToken)
    {
        SituacaoFaturaStripe situacao = i.Status switch
        {
            "paid" => SituacaoFaturaStripe.Paga,
            "void" or "uncollectible" => SituacaoFaturaStripe.Cancelada,
            _ => SituacaoFaturaStripe.Aberta
        };
        Guid? idContrato = i.Metadata is not null && i.Metadata.TryGetValue("id_contrato", out string? valor) && Guid.TryParse(valor, out Guid id) ? id : null;
        DateTime? fimDoPeriodo = i.Lines?.Data.Count > 0 ? i.Lines.Data.Max(l => l.Period.End) : null;

        return new FaturaGateway(
            i.Id,
            i.Parent?.SubscriptionDetails?.SubscriptionId,
            idContrato,
            DateOnly.FromDateTime(i.PeriodStart),
            fimDoPeriodo is DateTime fim ? DateOnly.FromDateTime(fim) : null,
            i.AmountDue / 100m,
            DateOnly.FromDateTime(i.DueDate ?? i.Created),
            situacao,
            i.HostedInvoiceUrl,
            i.StatusTransitions?.PaidAt,
            situacao == SituacaoFaturaStripe.Paga ? await FormaDoPagamentoAsync(idConta, i.Id, cancellationToken) : null);
    }

    private async Task<FormaPagamento?> FormaDoPagamentoAsync(string idConta, string idInvoice, CancellationToken cancellationToken)
    {
        StripeList<InvoicePayment> pagamentos = await new InvoicePaymentService(cliente).ListAsync(
            new InvoicePaymentListOptions { Invoice = idInvoice, Limit = 1 }, Opcoes(idConta), cancellationToken);
        string? idPagamento = pagamentos.Data.FirstOrDefault()?.Payment?.PaymentIntentId;
        if (idPagamento is null)
        {
            return FormaPagamento.Manual;
        }

        PaymentIntent pagamento = await new PaymentIntentService(cliente).GetAsync(
            idPagamento, new PaymentIntentGetOptions { Expand = ["latest_charge"] }, Opcoes(idConta), cancellationToken);
        return pagamento.LatestCharge?.PaymentMethodDetails?.Type switch
        {
            "boleto" => FormaPagamento.Boleto,
            "pix" => FormaPagamento.Pix,
            _ => FormaPagamento.Cartao
        };
    }
}

/// <summary>Validação do webhook com o segredo do endpoint (COB-06). Só lê os campos usados: a versão da API não importa aqui.</summary>
internal sealed class LeitorWebhookStripe(IOptions<StripeOptions> opcoes) : ILeitorWebhook
{
    public EventoGateway? Validar(string corpo, string assinatura)
    {
        string? segredo = opcoes.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(segredo))
        {
            return null;
        }

        try
        {
            EventUtility.ValidateSignature(corpo, assinatura, segredo);
            return Ler(corpo);
        }
        catch (Exception e) when (e is StripeException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    public EventoGateway Ler(string corpo)
    {
        using var documento = JsonDocument.Parse(corpo);
        JsonElement raiz = documento.RootElement;
        JsonElement objeto = raiz.GetProperty("data").GetProperty("object");
        return new EventoGateway(
            raiz.GetProperty("id").GetString()!,
            raiz.GetProperty("type").GetString()!,
            raiz.TryGetProperty("account", out JsonElement conta) ? conta.GetString() : null,
            objeto.TryGetProperty("id", out JsonElement id) ? id.GetString() : null,
            objeto.TryGetProperty("subscription", out JsonElement sub) && sub.ValueKind == JsonValueKind.String ? sub.GetString() : null);
    }
}
