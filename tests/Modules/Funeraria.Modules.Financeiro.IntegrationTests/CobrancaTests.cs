using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Application.Cobrancas;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Funeraria.Modules.Financeiro.Infrastructure.Stripe;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Notificacoes.Application.Email;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Financeiro.IntegrationTests;

public sealed class FinanceiroApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public ValueTask InitializeAsync() => IniciarAsync();
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<FinanceiroApiFactory>
{
    public const string Nome = "Api";
}

/// <summary>COB-01 a COB-09 com o gateway fake: do contrato à fatura, webhook, suspensão, baixa, multa e juros, reajuste.</summary>
[Collection(ApiCollection.Nome)]
public sealed class CobrancaTests(FinanceiroApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private GatewayFake Fake => factory.Services.GetRequiredService<GatewayFake>();

    [Fact]
    public async Task Contrato_espera_a_conta_e_depois_a_primeira_fatura_paga_ativa_o_contrato()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);
        Guid plano = await funeraria.CriarPlanoAsync(carenciaDias: 0);
        Guid idContrato = await cliente.ContratarAsync(plano);

        // Sem cadastro de pagamentos: o contrato existe e a assinatura espera.
        await AguardarAsync(async () => await AssinaturaAsync(funeraria.Tenant, idContrato) is { Status: StatusAssinatura.AguardandoConta }, "assinatura aguardando");
        Assert.Equal(HttpStatusCode.NotFound, (await funeraria.Equipe().GetAsync("/api/v1/gestao/pagamentos/conta", Ct)).StatusCode);

        string idConta = await CadastrarContaAsync(funeraria);
        JsonObject situacao = await Cenarios.LerAsync(await funeraria.Equipe().GetAsync("/api/v1/gestao/pagamentos/conta", Ct), HttpStatusCode.OK);
        Assert.True(situacao["cobrancas_habilitadas"]!.GetValue<bool>());

        // Quando a conta fica pronta, a assinatura sai e a fatura aparece para o cliente, com o link de pagamento.
        JsonObject fatura = (await cliente.Http.GetFromJsonAsync<JsonArray>($"/api/v1/contratos/{idContrato}/faturas", Json, Ct))![0]!.AsObject();
        Assert.Equal("PENDENTE", fatura["status"]!.GetValue<string>());
        Assert.Equal(109.80m, fatura["valor"]!.GetValue<decimal>());
        Assert.NotNull(fatura["link_pagamento"]);

        // Isolamento: outro cliente não vê as faturas nem a fatura.
        ClienteDeTeste outro = await funeraria.CriarClienteAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Http.GetAsync($"/api/v1/contratos/{idContrato}/faturas", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Http.GetAsync($"/api/v1/faturas/{fatura["id_fatura"]}", Ct)).StatusCode);

        Assert.Equal("A_PAGAR", await StatusContratoAsync(funeraria, idContrato));
        Assinatura assinatura = (await AssinaturaAsync(funeraria.Tenant, idContrato))!;
        string idInvoice = (await FaturasAsync(funeraria.Tenant, idContrato)).Single().IdStripeInvoice;
        Fake.Pagar(idInvoice, DateTime.UtcNow);

        // Pago pelo webhook assinado: a fatura fica paga e o contrato, ativo.
        Assert.Equal(HttpStatusCode.OK, (await EnviarWebhookAsync($"evt_{Guid.NewGuid():N}", TiposEventoStripe.FaturaPaga, idConta, idInvoice)).StatusCode);
        await AguardarAsync(async () => await StatusContratoAsync(funeraria, idContrato) == "ATIVO", "contrato ativo");
        Assert.Equal(StatusFatura.Paga, (await FaturasAsync(funeraria.Tenant, idContrato)).Single().Status);
        Assert.False(string.IsNullOrEmpty(assinatura.IdStripeSubscription));
    }

    [Fact]
    public async Task Webhook_recusa_assinatura_errada_e_processa_o_mesmo_evento_uma_vez()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        string idConta = await CadastrarContaAsync(funeraria);
        (_, Guid idContrato) = await ContratoComFaturaAsync(funeraria);
        string idInvoice = (await FaturasAsync(funeraria.Tenant, idContrato)).Single().IdStripeInvoice;
        string corpo = Evento("evt_invalido", TiposEventoStripe.FaturaPaga, idConta, idInvoice);

        using var semAssinatura = new StringContent(corpo, Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await factory.CreateClient().PostAsync("/api/v1/webhooks/stripe", semAssinatura, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostarAsync(corpo, "t=1,v1=assinatura-falsa")).StatusCode);

        string id = $"evt_{Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.OK, (await EnviarWebhookAsync(id, TiposEventoStripe.FaturaFinalizada, idConta, idInvoice)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await EnviarWebhookAsync(id, TiposEventoStripe.FaturaFinalizada, idConta, idInvoice)).StatusCode);
    }

    [Fact]
    public async Task Atraso_suspende_e_pagamento_cobra_encargos_e_reativa()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        string idConta = await CadastrarContaAsync(funeraria);
        (ClienteDeTeste cliente, Guid idContrato) = await ContratoComFaturaAsync(funeraria);
        Assinatura assinatura = (await AssinaturaAsync(funeraria.Tenant, idContrato))!;
        await PagarEAguardarAtivoAsync(funeraria, idConta, idContrato);
        (_, string emailGestor) = await factory.CriarUsuarioAsync((funeraria.Tenant, PerfilVinculo.Funeraria));
        HttpClient gestor = factory.Http(funeraria.Slug, await factory.EntrarAsync(funeraria.Slug, emailGestor));

        // Fatura vencida há 10 dias: estoura a tolerância em qualquer calendário de feriados.
        FaturaGateway vencida = Fake.EmitirFatura(assinatura.IdStripeSubscription!, DateOnly.FromDateTime(DateTime.Now).AddDays(-10), 89.90m);
        await EnviarWebhookAsync($"evt_{Guid.NewGuid():N}", TiposEventoStripe.FaturaFinalizada, idConta, vencida.IdInvoice);
        await AguardarAsync(async () => (await FaturasAsync(funeraria.Tenant, idContrato)).Count == 2, "segunda fatura");

        await RotinasAsync(funeraria.Tenant, (r, ct) => r.AplicarInadimplenciaAsync(funeraria.Tenant, ct));

        await AguardarAsync(async () => await StatusContratoAsync(funeraria, idContrato) == "SUSPENSO", "contrato suspenso");
        await AguardarNotificacaoAsync(gestor, "CONTRATO_SUSPENSO");
        await AguardarNotificacaoAsync(cliente.Http, "CONTRATO_SUSPENSO");

        // O cliente vê a multa e os juros antes de pagar: 2% + 1% ao mês por 10 dias sobre 89,90 = 2,10.
        Fatura local = (await FaturasAsync(funeraria.Tenant, idContrato)).Single(f => f.IdStripeInvoice == vencida.IdInvoice);
        JsonObject detalhe = (await cliente.Http.GetFromJsonAsync<JsonObject>($"/api/v1/faturas/{local.Id}", Json, Ct))!;
        Assert.Equal("VENCIDA", detalhe["status"]!.GetValue<string>());
        Assert.Equal(2.10m, detalhe["encargos_estimados"]!.GetValue<decimal>());

        // Pagou: reativa, e os encargos entram na próxima fatura do mensal, uma vez só.
        Fake.Pagar(vencida.IdInvoice, DateTime.UtcNow);
        await EnviarWebhookAsync($"evt_{Guid.NewGuid():N}", TiposEventoStripe.FaturaPaga, idConta, vencida.IdInvoice);
        await AguardarAsync(async () => await StatusContratoAsync(funeraria, idContrato) == "ATIVO", "contrato reativado");
        await AguardarNotificacaoAsync(cliente.Http, "CONTRATO_REATIVADO");
        CobrancaExtra encargo = Assert.Single(Fake.Extras, e => e.IdContrato == idContrato);
        Assert.Equal(2.10m, encargo.Valor);
        await AguardarAsync(async () => (await FaturasAsync(funeraria.Tenant, idContrato)).Single(f => f.Id == local.Id).ValorMultaJuros == 2.10m, "encargos gravados");
    }

    [Fact]
    public async Task Baixa_manual_exige_observacao_marca_no_stripe_e_nao_repete()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        await CadastrarContaAsync(funeraria);
        (ClienteDeTeste cliente, Guid idContrato) = await ContratoComFaturaAsync(funeraria);
        Fatura fatura = (await FaturasAsync(funeraria.Tenant, idContrato)).Single();
        string rota = $"/api/v1/gestao/faturas/{fatura.Id}/baixa";
        HttpClient funcionario = funeraria.Equipe(Perfis.Funcionario);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.Http.PostAsJsonAsync(rota, new { forma_pagamento = "MANUAL", observacao = "x" }, Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await funcionario.PostAsJsonAsync(rota, new { forma_pagamento = "MANUAL" }, Json, Ct)).StatusCode);

        JsonObject baixada = await Cenarios.LerAsync(
            await funcionario.PostAsJsonAsync(rota, new { forma_pagamento = "MANUAL", observacao = "Pago em dinheiro no balcão." }, Json, Ct), HttpStatusCode.OK);
        Assert.Equal("PAGA", baixada["status"]!.GetValue<string>());
        Assert.Contains($"paga-fora:{fatura.IdStripeInvoice}", Fake.Chamadas);
        Assert.Equal(HttpStatusCode.Conflict, (await funcionario.PostAsJsonAsync(rota, new { forma_pagamento = "MANUAL", observacao = "De novo." }, Json, Ct)).StatusCode);
        await AguardarAsync(async () => await StatusContratoAsync(funeraria, idContrato) == "ATIVO", "contrato ativo pela baixa");
    }

    [Fact]
    public async Task Dependentes_e_cancelamento_chegam_a_assinatura()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        await CadastrarContaAsync(funeraria);
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);
        Guid idContrato = await ContratoSoDoTitularAsync(cliente, await funeraria.CriarPlanoAsync(carenciaDias: 0));
        await AguardarAsync(async () => await AssinaturaAsync(funeraria.Tenant, idContrato) is { Status: StatusAssinatura.Ativa }, "assinatura ativa");
        string sub = (await AssinaturaAsync(funeraria.Tenant, idContrato))!.IdStripeSubscription!;

        await cliente.Http.PostAsJsonAsync($"/api/v1/contratos/{idContrato}/dependentes", new { id_dependente = cliente.Dependentes[0] }, Json, Ct);
        await AguardarAsync(() => Task.FromResult(Fake.Chamadas.Contains($"dependentes:{sub}:1")), "quantidade atualizada no Stripe");

        await cliente.Http.PostAsJsonAsync($"/api/v1/contratos/{idContrato}/cancelar", new { motivo = "Mudança de cidade." }, Json, Ct);
        await AguardarAsync(() => Task.FromResult(Fake.Chamadas.Contains($"cancelar:{sub}")), "assinatura cancelada no Stripe");
        await AguardarAsync(async () => (await FaturasAsync(funeraria.Tenant, idContrato)).Single().Status == StatusFatura.Cancelada, "fatura futura cancelada");
    }

    [Fact]
    public async Task Lembrete_sai_uma_vez_e_reajuste_avisa_antes_e_aplica_no_aniversario()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        await CadastrarContaAsync(funeraria);
        (ClienteDeTeste cliente, Guid idContrato) = await ContratoComFaturaAsync(funeraria);
        Assinatura assinatura = (await AssinaturaAsync(funeraria.Tenant, idContrato))!;

        // A fatura do fake vence em 5 dias; ajusta para hoje, que sempre gera lembrete.
        FaturaGateway hoje = Fake.EmitirFatura(assinatura.IdStripeSubscription!, DateOnly.FromDateTime(DateTime.Now), 89.90m);
        await RotinasAsync(funeraria.Tenant, (r, ct) => r.ReconciliarAsync(funeraria.Tenant, ct));
        Assert.Contains(await FaturasAsync(funeraria.Tenant, idContrato), f => f.IdStripeInvoice == hoje.IdInvoice);

        await RotinasAsync(funeraria.Tenant, (r, ct) => r.EnviarLembretesAsync(funeraria.Tenant, ct));
        await RotinasAsync(funeraria.Tenant, (r, ct) => r.EnviarLembretesAsync(funeraria.Tenant, ct));
        EmailMensagem lembrete = await factory.Emails.AguardarAsync(EmailDoTitular(cliente), e => e.Assunto.Contains("vence hoje", StringComparison.Ordinal));
        Assert.Contains("https://pagar.stripe.test/fatura", lembrete.CorpoHtml, StringComparison.Ordinal);
        await Task.Delay(1000, Ct);
        Assert.Single(factory.Emails.Para(EmailDoTitular(cliente)), e => e.Assunto.Contains("vence hoje", StringComparison.Ordinal));

        // Aniversário no passado: reajuste de 5%, arredondado, uma vez só.
        await RetrocederContratacaoAsync(assinatura.Id, funeraria.Tenant, anos: 1);
        await RotinasAsync(funeraria.Tenant, (r, ct) => r.AplicarReajustesAsync(funeraria.Tenant, ct));
        await RotinasAsync(funeraria.Tenant, (r, ct) => r.AplicarReajustesAsync(funeraria.Tenant, ct));
        Assert.Single(Fake.Chamadas, c => c.StartsWith($"reajuste:{assinatura.IdStripeSubscription}:", StringComparison.Ordinal));
        Assert.Contains($"reajuste:{assinatura.IdStripeSubscription}:{94.40m.ToString(CultureInfo.CurrentCulture)}:{20.90m.ToString(CultureInfo.CurrentCulture)}", Fake.Chamadas);
        await AguardarAsync(
            async () => (await funeraria.Equipe().GetFromJsonAsync<JsonObject>($"/api/v1/contratos/{idContrato}", Json, Ct))!["valor_plano"]!.GetValue<decimal>() == 94.40m,
            "contrato com o valor novo");
    }

    private static string EmailDoTitular(ClienteDeTeste cliente) => cliente.Email;

    private async Task<string> CadastrarContaAsync(FunerariaDeTeste funeraria)
    {
        JsonObject link = await Cenarios.LerAsync(
            await funeraria.Equipe().PostAsJsonAsync("/api/v1/gestao/pagamentos/conta/cadastro",
                new { url_retorno = "https://demo.localhost/pagamentos/pronto", url_renovar = "https://demo.localhost/pagamentos/renovar" }, Json, Ct),
            HttpStatusCode.OK);
        Assert.StartsWith("https://", link["url"]!.GetValue<string>(), StringComparison.Ordinal);
        string idConta = $"acct_{funeraria.Tenant:N}";
        Fake.LiberarConta(idConta);
        await funeraria.Equipe().GetAsync("/api/v1/gestao/pagamentos/conta", Ct);
        return idConta;
    }

    private async Task<(ClienteDeTeste Cliente, Guid IdContrato)> ContratoComFaturaAsync(FunerariaDeTeste funeraria)
    {
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        Guid idContrato = await cliente.ContratarAsync(await funeraria.CriarPlanoAsync(carenciaDias: 0));
        await AguardarAsync(async () => (await FaturasAsync(funeraria.Tenant, idContrato)).Count == 1, "primeira fatura");
        return (cliente, idContrato);
    }

    private static async Task<Guid> ContratoSoDoTitularAsync(ClienteDeTeste cliente, Guid plano)
    {
        HttpResponseMessage response = await cliente.Http.PostAsJsonAsync("/api/v1/contratos",
            new { id_plano = plano, tipo_cobertura = "TITULAR", periodicidade = "MENSAL" }, Json, Ct);
        return (await Cenarios.LerAsync(response, HttpStatusCode.Created))["id_contrato"]!.GetValue<Guid>();
    }

    private async Task PagarEAguardarAtivoAsync(FunerariaDeTeste funeraria, string idConta, Guid idContrato)
    {
        string idInvoice = (await FaturasAsync(funeraria.Tenant, idContrato)).Single().IdStripeInvoice;
        Fake.Pagar(idInvoice, DateTime.UtcNow);
        await EnviarWebhookAsync($"evt_{Guid.NewGuid():N}", TiposEventoStripe.FaturaPaga, idConta, idInvoice);
        await AguardarAsync(async () => await StatusContratoAsync(funeraria, idContrato) == "ATIVO", "contrato ativo");
    }

    private static async Task<string> StatusContratoAsync(FunerariaDeTeste funeraria, Guid idContrato) =>
        (await funeraria.Equipe().GetFromJsonAsync<JsonObject>($"/api/v1/contratos/{idContrato}", Json, Ct))!["status"]!.GetValue<string>();

    private async Task<Assinatura?> AssinaturaAsync(Guid tenant, Guid idContrato)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        return await scope.ServiceProvider.GetRequiredService<IAssinaturaRepository>().ObterDoContratoAsync(idContrato, Ct);
    }

    private async Task<IReadOnlyList<Fatura>> FaturasAsync(Guid tenant, Guid idContrato)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        return await scope.ServiceProvider.GetRequiredService<IFaturaRepository>().ListarDoContratoAsync(idContrato, Ct);
    }

    private async Task RotinasAsync(Guid tenant, Func<RotinasDeCobranca, CancellationToken, Task> rotina)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        await rotina(scope.ServiceProvider.GetRequiredService<RotinasDeCobranca>(), Ct);
    }

    private async Task RetrocederContratacaoAsync(Guid idAssinatura, Guid tenant, int anos)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        var context = scope.ServiceProvider.GetRequiredService<Funeraria.Modules.Financeiro.Infrastructure.Database.FinanceiroDbContext>();
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlAsync(
            context.Database, $"UPDATE financeiro.assinaturas SET data_contratacao = data_contratacao - make_interval(years => {anos}) WHERE id_assinatura = {idAssinatura}", Ct);
    }

    private Task<HttpResponseMessage> EnviarWebhookAsync(string id, string tipo, string conta, string idObjeto)
    {
        string corpo = Evento(id, tipo, conta, idObjeto);
        long agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        byte[] assinatura = HMACSHA256.HashData(Encoding.UTF8.GetBytes(FunerariaApiFactory.SegredoWebhookStripe), Encoding.UTF8.GetBytes($"{agora}.{corpo}"));
        return PostarAsync(corpo, $"t={agora},v1={Convert.ToHexStringLower(assinatura)}");
    }

    private Task<HttpResponseMessage> PostarAsync(string corpo, string assinatura)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Stripe-Signature", assinatura);
        return client.PostAsync("/api/v1/webhooks/stripe", new StringContent(corpo, Encoding.UTF8, "application/json"), Ct);
    }

    private static string Evento(string id, string tipo, string conta, string idObjeto) =>
        JsonSerializer.Serialize(new { id, @object = "event", type = tipo, account = conta, data = new { @object = new { id = idObjeto } } });

    private static Task AguardarNotificacaoAsync(HttpClient http, string tipo) =>
        AguardarAsync(
            async () => (await http.GetFromJsonAsync<JsonObject>("/api/v1/notificacoes", Json, Ct))!["itens"]!.AsArray().Any(n => n!["tipo"]!.GetValue<string>() == tipo),
            $"notificação {tipo}");

    private static async Task AguardarAsync(Func<Task<bool>> condicao, string oQue)
    {
        for (int i = 0; i < 100; i++)
        {
            if (await condicao())
            {
                return;
            }

            await Task.Delay(200, Ct);
        }

        throw new TimeoutException($"Esperando: {oQue}.");
    }
}
