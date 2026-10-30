using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Application.Cobrancas;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Funeraria.Modules.Financeiro.Infrastructure.Stripe;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Financeiro.IntegrationTests;

/// <summary>FUN-04: orçamento dos adicionais, aprovação e fatura avulsa, que não suspende o plano.</summary>
[Collection(ApiCollection.Nome)]
public sealed class OrcamentoFuneralTests(FinanceiroApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private GatewayFake Fake => factory.Services.GetRequiredService<GatewayFake>();

    [Fact]
    public async Task Sem_conta_a_aprovacao_vale_e_a_fatura_sai_uma_vez_quando_a_conta_fica_pronta()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Cenario c = await FuneralAsync(funeraria);
        await AdicionarAsync(c, await funeraria.CriarItemCatalogoAsync("Coroa", 300m), 2);

        JsonObject orcamento = await Cenarios.LerAsync(await GerarAsync(c), HttpStatusCode.Created);
        Assert.Equal("PENDENTE", orcamento["status"]!.GetValue<string>());
        Assert.Equal(600m, orcamento["total"]!.GetValue<decimal>());
        Guid id = orcamento["id_orcamento"]!.GetValue<Guid>();

        // Faltou dizer quem aprovou; outro cliente nem enxerga; a equipe registra.
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Equipe.PostAsJsonAsync($"{c.Rota}/orcamento/aprovar", new { id_orcamento = id }, Json, Ct)).StatusCode);
        ClienteDeTeste outro = await funeraria.CriarClienteAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Http.GetAsync($"{c.Rota}/orcamento", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AprovarAsync(outro.Http, c, id)).StatusCode);

        JsonObject aprovado = await Cenarios.LerAsync(await AprovarAsync(c.Equipe, c, id), HttpStatusCode.OK);
        Assert.Equal("APROVADO", aprovado["status"]!.GetValue<string>());
        Assert.Equal("Ana Silva", aprovado["aprovado_por"]!.GetValue<string>());
        Assert.NotNull(aprovado["aprovado_em"]);
        Assert.True(aprovado["fatura_pendente"]!.GetValue<bool>());
        Assert.Null(aprovado["id_fatura"]);

        await CadastrarContaAsync(funeraria);
        JsonObject comFatura = await Cenarios.LerAsync(await AprovarAsync(c.Equipe, c, id), HttpStatusCode.OK);
        Assert.False(comFatura["fatura_pendente"]!.GetValue<bool>());
        Assert.NotNull(comFatura["link_pagamento"]);

        // O titular acompanha; aprovar de novo não emite outra.
        JsonObject visto = (await c.Cliente.Http.GetFromJsonAsync<JsonObject>($"{c.Rota}/orcamento", Json, Ct))!;
        Assert.Equal(comFatura["id_fatura"]!.GetValue<Guid>(), visto["id_fatura"]!.GetValue<Guid>());
        await Cenarios.LerAsync(await AprovarAsync(c.Cliente.Http, c, id), HttpStatusCode.OK);
        CobrancaExtra cobranca = Assert.Single(Fake.Extras, e => e.ChaveIdempotencia == $"orcamento-{id}");
        Assert.Equal(600m, cobranca.Valor);
        Assert.DoesNotContain(c.NomeFalecido, cobranca.Descricao, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mudar_os_itens_invalida_o_pendente_e_o_novo_orcamento_cobra_so_a_diferenca()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        await CadastrarContaAsync(funeraria);
        Cenario c = await FuneralAsync(funeraria);
        Assert.Equal(HttpStatusCode.Conflict, (await GerarAsync(c)).StatusCode);
        Guid coroa = await funeraria.CriarItemCatalogoAsync("Coroa", 300m);
        Guid idCoroa = await AdicionarAsync(c, coroa, 1);

        Guid velho = (await Cenarios.LerAsync(await GerarAsync(c), HttpStatusCode.Created))["id_orcamento"]!.GetValue<Guid>();
        Guid idVela = await AdicionarAsync(c, await funeraria.CriarItemCatalogoAsync("Velas", 50m), 2);
        Assert.Equal(HttpStatusCode.Conflict, (await AprovarAsync(c.Equipe, c, velho)).StatusCode);
        Assert.Equal("SUBSTITUIDO", (await c.Equipe.GetFromJsonAsync<JsonObject>($"{c.Rota}/orcamento", Json, Ct))!["status"]!.GetValue<string>());

        JsonObject atual = await Cenarios.LerAsync(await GerarAsync(c), HttpStatusCode.Created);
        Assert.Equal(400m, atual["total"]!.GetValue<decimal>());
        await Cenarios.LerAsync(await AprovarAsync(c.Equipe, c, atual["id_orcamento"]!.GetValue<Guid>()), HttpStatusCode.OK);

        // Item aprovado não sai; um novo entra num orçamento só dele.
        Assert.Equal(HttpStatusCode.Conflict, (await c.Equipe.DeleteAsync($"{c.Rota}/itens/{idCoroa}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.Equipe.DeleteAsync($"{c.Rota}/itens/{idVela}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await GerarAsync(c)).StatusCode);
        await AdicionarAsync(c, coroa, 1);
        JsonObject diferenca = await Cenarios.LerAsync(await GerarAsync(c), HttpStatusCode.Created);
        Assert.Equal(300m, diferenca["total"]!.GetValue<decimal>());
        Assert.Single(diferenca["itens"]!.AsArray());
    }

    [Fact]
    public async Task Fatura_vencida_nao_suspende_o_plano_e_cancelar_o_funeral_cancela_a_fatura()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        string idConta = await CadastrarContaAsync(funeraria);
        Cenario c = await FuneralAsync(funeraria);
        await AdicionarAsync(c, await funeraria.CriarItemCatalogoAsync("Coroa", 300m), 1);
        Guid id = (await Cenarios.LerAsync(await GerarAsync(c), HttpStatusCode.Created))["id_orcamento"]!.GetValue<Guid>();
        JsonObject aprovado = await Cenarios.LerAsync(await AprovarAsync(c.Equipe, c, id), HttpStatusCode.OK);
        Guid idFatura = aprovado["id_fatura"]!.GetValue<Guid>();

        // Só a fatura do orçamento em atraso, bem depois da tolerância: a fatura vence, o plano fica como está.
        await QuitarFaturasDoPlanoAsync(funeraria, idConta, c.IdContrato, idFatura);
        Fatura avulsa = (await FaturasAsync(funeraria.Tenant, c.IdContrato)).Single(f => f.Id == idFatura);
        Assert.True(avulsa.ForaDaInadimplencia);
        Fake.Vencer(avulsa.IdStripeInvoice, DateOnly.FromDateTime(DateTime.Now).AddDays(-40));
        await EnviarWebhookAsync($"evt_{Guid.NewGuid():N}", TiposEventoStripe.FaturaAtualizada, idConta, avulsa.IdStripeInvoice);
        await AguardarAsync(async () => (await FaturasAsync(funeraria.Tenant, c.IdContrato)).Single(f => f.Id == idFatura).DataVencimento < DateOnly.FromDateTime(DateTime.Now), "vencimento atualizado");
        await RotinasAsync(funeraria.Tenant, (r, ct) => r.AplicarInadimplenciaAsync(funeraria.Tenant, ct));

        Assert.Equal(StatusFatura.Vencida, (await FaturasAsync(funeraria.Tenant, c.IdContrato)).Single(f => f.Id == idFatura).Status);
        Assert.False((await AssinaturaAsync(funeraria.Tenant, c.IdContrato))!.Inadimplente);

        await c.Equipe.PatchAsJsonAsync($"{c.Rota}/status", new { status = "CANCELADO" }, Json, Ct);
        await AguardarAsync(
            async () => (await FaturasAsync(funeraria.Tenant, c.IdContrato)).Single(f => f.Id == idFatura).Status == StatusFatura.Cancelada,
            "fatura do orçamento cancelada");
    }

    private sealed record Cenario(string Rota, HttpClient Equipe, ClienteDeTeste Cliente, Guid IdContrato, string NomeFalecido);

    private async Task<Cenario> FuneralAsync(FunerariaDeTeste funeraria)
    {
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        Guid idContrato = await cliente.ContratarAsync(await funeraria.CriarPlanoAsync(carenciaDias: 0));
        await factory.AtivarContratoAsync(idContrato);
        HttpClient equipe = funeraria.Equipe();
        JsonObject contrato = (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/contratos/{idContrato}", Json, Ct))!;
        JsonNode titular = contrato["pessoas_cobertas"]!.AsArray().Single(p => p!["tipo"]!.GetValue<string>() == "TITULAR")!;
        Guid idBeneficiario = (await Cenarios.LerAsync(await equipe.PostAsJsonAsync("/api/v1/beneficiarios", new
        {
            id_pessoa_coberta = titular["id_pessoa_coberta"]!.GetValue<Guid>(),
            data_falecimento = DateOnly.FromDateTime(DateTime.Now),
            local_falecimento = "Hospital Central",
            responsavel = new { nome = "Ana Silva", telefone = "(44) 98888-7777", parentesco = "Filha" }
        }, Json, Ct), HttpStatusCode.Created))["id_beneficiario"]!.GetValue<Guid>();
        Guid idFuneral = (await Cenarios.LerAsync(await equipe.PostAsJsonAsync("/api/v1/funerais",
            new { id_beneficiario = idBeneficiario, data_hora_prevista = DateTime.UtcNow.AddDays(1), destinacao = "SEPULTAMENTO" }, Json, Ct), HttpStatusCode.Created))["id_funeral"]!.GetValue<Guid>();
        return new Cenario($"/api/v1/funerais/{idFuneral}", equipe, cliente, idContrato, titular["nome"]?.GetValue<string>() ?? "Sem nome informado");
    }

    private static async Task<Guid> AdicionarAsync(Cenario c, Guid item, int quantidade)
    {
        JsonObject itens = await Cenarios.LerAsync(
            await c.Equipe.PostAsJsonAsync($"{c.Rota}/itens", new { id_item_catalogo = item, quantidade }, Json, Ct), HttpStatusCode.OK);
        return itens["itens_adicionais"]!.AsArray().Last(i => i!["id_item_catalogo"]!.GetValue<Guid>() == item)!["id_funeral_item"]!.GetValue<Guid>();
    }

    private static Task<HttpResponseMessage> GerarAsync(Cenario c) => c.Equipe.PostAsync($"{c.Rota}/orcamento", null, Ct);

    private static Task<HttpResponseMessage> AprovarAsync(HttpClient http, Cenario c, Guid id) =>
        http.PostAsJsonAsync($"{c.Rota}/orcamento/aprovar", new { id_orcamento = id, nome_responsavel = "Ana Silva" }, Json, Ct);

    private async Task<string> CadastrarContaAsync(FunerariaDeTeste funeraria)
    {
        await Cenarios.LerAsync(
            await funeraria.Equipe().PostAsJsonAsync("/api/v1/gestao/pagamentos/conta/cadastro",
                new { url_retorno = "https://demo.localhost/pronto", url_renovar = "https://demo.localhost/renovar" }, Json, Ct),
            HttpStatusCode.OK);
        string idConta = $"acct_{funeraria.Tenant:N}";
        Fake.LiberarConta(idConta);
        await funeraria.Equipe().GetAsync("/api/v1/gestao/pagamentos/conta", Ct);
        return idConta;
    }

    private async Task QuitarFaturasDoPlanoAsync(FunerariaDeTeste funeraria, string idConta, Guid idContrato, Guid exceto)
    {
        foreach (Fatura fatura in (await FaturasAsync(funeraria.Tenant, idContrato)).Where(f => f.Id != exceto))
        {
            Fake.Pagar(fatura.IdStripeInvoice, DateTime.UtcNow);
            await EnviarWebhookAsync($"evt_{Guid.NewGuid():N}", TiposEventoStripe.FaturaPaga, idConta, fatura.IdStripeInvoice);
        }
    }

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

    private Task<HttpResponseMessage> EnviarWebhookAsync(string id, string tipo, string conta, string idObjeto)
    {
        string corpo = JsonSerializer.Serialize(new { id, @object = "event", type = tipo, account = conta, data = new { @object = new { id = idObjeto } } });
        long agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        byte[] assinatura = HMACSHA256.HashData(Encoding.UTF8.GetBytes(FunerariaApiFactory.SegredoWebhookStripe), Encoding.UTF8.GetBytes($"{agora}.{corpo}"));
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Stripe-Signature", $"t={agora},v1={Convert.ToHexStringLower(assinatura)}");
        return client.PostAsync("/api/v1/webhooks/stripe", new StringContent(corpo, Encoding.UTF8, "application/json"), Ct);
    }

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
