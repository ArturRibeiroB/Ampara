using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Application.Plataforma;
using Funeraria.Modules.Financeiro.Domain.Plataforma;
using Funeraria.Modules.Financeiro.Infrastructure.Stripe;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Financeiro.IntegrationTests;

/// <summary>
/// DEC-09 e DEC-11: a conta liberada ativa a funerária e começa a mensalidade da plataforma; as faturas da conta da plataforma
/// chegam pelo webhook; o ADMIN muda valores e taxa sem deploy.
/// </summary>
[Collection(ApiCollection.Nome)]
public sealed class PlataformaTests(FinanceiroApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CobrancaPlataformaFake Plataforma => factory.Services.GetRequiredService<CobrancaPlataformaFake>();

    [Fact]
    public async Task Conta_liberada_ativa_a_funeraria_pendente_e_comeca_a_mensalidade()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync(StatusFuneraria.Pendente);
        var funeraria = new FunerariaDeTeste(factory, tenant, slug);
        Assert.Equal(HttpStatusCode.NotFound, (await Gestor(funeraria).GetAsync("/api/v1/gestao/pagamentos/mensalidade", Ct)).StatusCode);

        await LiberarContaAsync(funeraria);

        Assert.Equal("Ativa", await ConsultarAsync<string>("SELECT status FROM tenants.funerarias WHERE id_tenant = @tenant", new { tenant }));
        JsonObject mensalidade = await Cenarios.LerAsync(await Gestor(funeraria).GetAsync("/api/v1/gestao/pagamentos/mensalidade", Ct), HttpStatusCode.OK);
        Assert.Equal("ATIVA", mensalidade["status"]!.GetValue<string>());
        decimal valorPadrao = (await Cenarios.LerAsync(await Admin().GetAsync("/api/v1/admin/plataforma/cobranca", Ct), HttpStatusCode.OK))["valor_mensalidade"]!.GetValue<decimal>();
        Assert.Equal(valorPadrao, mensalidade["valor"]!.GetValue<decimal>());
        Assert.Single(Plataforma.Chamadas, c => c.StartsWith($"assinatura:sub_plataforma_{tenant:N}:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Funeraria_suspensa_continua_suspensa_quando_a_conta_e_liberada()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync(StatusFuneraria.Pendente);
        await ExecutarAsync("UPDATE tenants.funerarias SET status = 'Suspensa' WHERE id_tenant = @tenant", new { tenant });

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
            var eventBus = scope.ServiceProvider.GetRequiredService<AvisoContaLiberada>();
            await eventBus.PublicarAsync(tenant, Ct);
        }

        Assert.Equal("Suspensa", await ConsultarAsync<string>("SELECT status FROM tenants.funerarias WHERE id_tenant = @tenant", new { tenant }));
    }

    [Fact]
    public async Task Fatura_da_mensalidade_chega_pelo_webhook_da_conta_da_plataforma()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        await LiberarContaAsync(funeraria);
        string idSubscription = Plataforma.AssinaturaDa(funeraria.Tenant)!;

        FaturaPlataforma atrasada = Plataforma.EmitirFatura(idSubscription, DateOnly.FromDateTime(DateTime.Now).AddDays(-3), 149m);
        await EnviarWebhookAsync("invoice.finalized", atrasada.IdInvoice);
        JsonObject inadimplente = await AguardarMensalidadeAsync(funeraria, "INADIMPLENTE");
        Assert.Equal("ABERTA", inadimplente["ultima_fatura"]!["situacao"]!.GetValue<string>());
        Assert.Equal("https://pagar.stripe.test/mensalidade", inadimplente["ultima_fatura"]!["link_pagamento"]!.GetValue<string>());

        Plataforma.Pagar(atrasada.IdInvoice, DateTime.UtcNow);
        await EnviarWebhookAsync("invoice.paid", atrasada.IdInvoice);
        JsonObject emDia = await AguardarMensalidadeAsync(funeraria, "ATIVA");
        Assert.Equal("PAGA", emDia["ultima_fatura"]!["situacao"]!.GetValue<string>());
        Assert.NotNull(emDia["ultimo_pagamento_em"]);
    }

    [Fact]
    public async Task Admin_muda_mensalidade_e_taxa_e_define_taxa_propria_de_uma_funeraria()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        string idConta = await LiberarContaAsync(funeraria);
        string idSubscription = Plataforma.AssinaturaDa(funeraria.Tenant)!;
        JsonObject original = await Cenarios.LerAsync(await Admin().GetAsync("/api/v1/admin/plataforma/cobranca", Ct), HttpStatusCode.OK);

        try
        {
            Assert.Equal(HttpStatusCode.Forbidden,
                (await Gestor(funeraria).PutAsJsonAsync("/api/v1/admin/plataforma/cobranca", new { valor_mensalidade = 1, taxa_percentual = 0 }, Json, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await Admin().PutAsJsonAsync("/api/v1/admin/plataforma/cobranca", new { valor_mensalidade = 199, taxa_percentual = 25 }, Json, Ct)).StatusCode);

            JsonObject alterada = await Cenarios.LerAsync(
                await Admin().PutAsJsonAsync("/api/v1/admin/plataforma/cobranca", new { valor_mensalidade = 199.90m, taxa_percentual = 3 }, Json, Ct),
                HttpStatusCode.OK);
            Assert.Equal(199.90m, alterada["valor_mensalidade"]!.GetValue<decimal>());

            // A rotina diária leva o valor novo para a assinatura no Stripe.
            await RotinaAsync();
            Assert.Contains($"valor:{idSubscription}:{199.90m.ToString(CultureInfo.CurrentCulture)}", Plataforma.Chamadas);
            Assert.Equal(199.90m, (await Cenarios.LerAsync(await Gestor(funeraria).GetAsync("/api/v1/gestao/pagamentos/mensalidade", Ct), HttpStatusCode.OK))["valor"]!.GetValue<decimal>());
            Assert.Equal(3m, await TaxaAsync(idConta));

            JsonObject propria = await Cenarios.LerAsync(
                await Admin().PutAsJsonAsync($"/api/v1/admin/tenants/{funeraria.Tenant}/taxa-plataforma", new { taxa_percentual = 1.5m }, Json, Ct),
                HttpStatusCode.OK);
            Assert.Equal(1.5m, propria["taxa_vigente"]!.GetValue<decimal>());
            Assert.Equal(1.5m, await TaxaAsync(idConta));

            JsonObject padrao = await Cenarios.LerAsync(
                await Admin().PutAsJsonAsync($"/api/v1/admin/tenants/{funeraria.Tenant}/taxa-plataforma", new { taxa_percentual = (decimal?)null }, Json, Ct),
                HttpStatusCode.OK);
            Assert.Equal(3m, padrao["taxa_vigente"]!.GetValue<decimal>());

            JsonArray lista = (await Admin().GetFromJsonAsync<JsonArray>("/api/v1/admin/plataforma/mensalidades", Json, Ct))!;
            Assert.Contains(lista, m => m!["id_tenant"]!.GetValue<Guid>() == funeraria.Tenant);
        }
        finally
        {
            // A configuração é da plataforma inteira: volta como estava para os outros testes.
            await Admin().PutAsJsonAsync("/api/v1/admin/plataforma/cobranca",
                new { valor_mensalidade = original["valor_mensalidade"]!.GetValue<decimal>(), taxa_percentual = original["taxa_percentual"]!.GetValue<decimal>() }, Json, Ct);
            await RotinaAsync();
        }
    }

    private async Task<string> LiberarContaAsync(FunerariaDeTeste funeraria)
    {
        await Cenarios.LerAsync(
            await Gestor(funeraria).PostAsJsonAsync("/api/v1/gestao/pagamentos/conta/cadastro",
                new { url_retorno = "https://demo.localhost/pronto", url_renovar = "https://demo.localhost/renovar" }, Json, Ct),
            HttpStatusCode.OK);
        string idConta = $"acct_{funeraria.Tenant:N}";
        factory.Services.GetRequiredService<GatewayFake>().LiberarConta(idConta);
        await Gestor(funeraria).GetAsync("/api/v1/gestao/pagamentos/conta", Ct);
        return idConta;
    }

    private async Task<decimal> TaxaAsync(string idConta)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TaxaDaPlataforma>().DaContaAsync(idConta, Ct);
    }

    private async Task RotinaAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetServices<IJobRecorrente>().Single(j => j.Id == "financeiro-mensalidade-plataforma").ExecutarAsync(Ct);
    }

    private async Task<JsonObject> AguardarMensalidadeAsync(FunerariaDeTeste funeraria, string status)
    {
        for (int i = 0; i < 75; i++)
        {
            JsonObject atual = (await Gestor(funeraria).GetFromJsonAsync<JsonObject>("/api/v1/gestao/pagamentos/mensalidade", Json, Ct))!;
            if (atual["status"]!.GetValue<string>() == status && atual["ultima_fatura"] is not null)
            {
                return atual;
            }

            await Task.Delay(200, Ct);
        }

        throw new TimeoutException($"Esperando a mensalidade {status}.");
    }

    // Evento da conta da plataforma: sem o campo account.
    private Task<HttpResponseMessage> EnviarWebhookAsync(string tipo, string idObjeto)
    {
        string corpo = JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            @object = "event",
            type = tipo,
            data = new { @object = new { id = idObjeto } }
        });
        long agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        byte[] assinatura = HMACSHA256.HashData(Encoding.UTF8.GetBytes(FunerariaApiFactory.SegredoWebhookStripe), Encoding.UTF8.GetBytes($"{agora}.{corpo}"));
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Stripe-Signature", $"t={agora},v1={Convert.ToHexStringLower(assinatura)}");
        return client.PostAsync("/api/v1/webhooks/stripe", new StringContent(corpo, Encoding.UTF8, "application/json"), Ct);
    }

    private HttpClient Gestor(FunerariaDeTeste funeraria) => funeraria.Equipe(Perfis.Funeraria);

    private HttpClient Admin()
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(Perfis.Admin, null));
        return client;
    }

    private async Task<T> ConsultarAsync<T>(string sql, object parametros)
    {
        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        return await conexao.QuerySingleAsync<T>(new CommandDefinition(sql, parametros, cancellationToken: Ct));
    }

    private async Task ExecutarAsync(string sql, object parametros)
    {
        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        await conexao.ExecuteAsync(new CommandDefinition(sql, parametros, cancellationToken: Ct));
    }
}
