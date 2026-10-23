using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Terceiros.Contracts;
using Funeraria.Modules.Terceiros.Contracts.IntegrationEvents;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Terceiros.IntegrationTests;

/// <summary>TER-02: lista única de atribuições, aceite, recusa e isolamento entre terceiros.</summary>
[Collection(ApiCollection.Nome)]
public sealed class AtribuicoesTests(TerceirosApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Terceiro_ve_so_o_resumo_aceita_e_a_origem_e_avisada()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        Guid frota = await CadastrarAsync(tenant, slug, "FROTA");
        Guid origem = Guid.NewGuid();
        Guid id = (await AtribuirAsync(tenant, new NovaAtribuicao(frota, origem, Translado()))).Value;
        HttpClient terceiro = Terceiro(tenant, slug, frota, TiposTerceiro.Frota);

        JsonNode item = Assert.Single((await terceiro.GetFromJsonAsync<JsonArray>("/api/v1/terceiros/atribuicoes", Json, Ct))!)!;
        Assert.Equal("TRANSLADO", item["tipo"]!.GetValue<string>());
        Assert.Equal("PENDENTE", item["status"]!.GetValue<string>());
        Assert.Equal("Hospital Central", item["resumo"]!["origem"]!.GetValue<string>());
        Assert.Equal("Ana Lima", item["resumo"]!["contato"]!["nome"]!.GetValue<string>());
        Assert.DoesNotContain("cpf", item["resumo"]!.ToJsonString(), StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage aceite = await terceiro.PostAsync($"/api/v1/terceiros/atribuicoes/{id}/aceite", null, Ct);
        Assert.Equal(HttpStatusCode.OK, aceite.StatusCode);
        Assert.Equal("ACEITA", (await aceite.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["status"]!.GetValue<string>());

        AtribuicaoAceitaIntegrationEvent evento = await factory.Eventos.AguardarAsync<AtribuicaoAceitaIntegrationEvent>(e => e.IdAtribuicao == id);
        Assert.Equal(origem, evento.IdOrigem);
        Assert.Equal("TRANSLADO", evento.Tipo);

        HttpResponseMessage deNovo = await terceiro.PostAsync($"/api/v1/terceiros/atribuicoes/{id}/aceite", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, deNovo.StatusCode);
    }

    [Fact]
    public async Task Recusa_exige_motivo_e_avisa_a_origem()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        Guid fornecedor = await CadastrarAsync(tenant, slug, "FORNECEDOR");
        Guid id = (await AtribuirAsync(tenant, new NovaAtribuicao(fornecedor, Guid.NewGuid(), Pedido()))).Value;
        HttpClient terceiro = Terceiro(tenant, slug, fornecedor, TiposTerceiro.Fornecedor);

        HttpResponseMessage semMotivo = await terceiro.PostAsJsonAsync($"/api/v1/terceiros/atribuicoes/{id}/recusa", new { motivo = "" }, Json, Ct);
        HttpResponseMessage recusa = await terceiro.PostAsJsonAsync($"/api/v1/terceiros/atribuicoes/{id}/recusa", new { motivo = "Sem estoque de coroas." }, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, semMotivo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recusa.StatusCode);
        AtribuicaoRecusadaIntegrationEvent evento = await factory.Eventos.AguardarAsync<AtribuicaoRecusadaIntegrationEvent>(e => e.IdAtribuicao == id);
        Assert.Equal("Sem estoque de coroas.", evento.Motivo);
    }

    [Fact]
    public async Task Um_terceiro_nao_ve_nem_responde_atribuicao_de_outro()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        Guid dono = await CadastrarAsync(tenant, slug, "FROTA");
        Guid outro = await CadastrarAsync(tenant, slug, "FROTA");
        Guid id = (await AtribuirAsync(tenant, new NovaAtribuicao(dono, Guid.NewGuid(), Translado()))).Value;
        HttpClient intruso = Terceiro(tenant, slug, outro, TiposTerceiro.Frota);

        Assert.Empty((await intruso.GetFromJsonAsync<JsonArray>("/api/v1/terceiros/atribuicoes", Json, Ct))!);
        Assert.Equal(HttpStatusCode.NotFound, (await intruso.PostAsync($"/api/v1/terceiros/atribuicoes/{id}/aceite", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await intruso.PostAsJsonAsync($"/api/v1/terceiros/atribuicoes/{id}/recusa", new { motivo = "x" }, Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Prazo_vencido_aparece_como_expirada_e_nao_aceita_mais()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        Guid frota = await CadastrarAsync(tenant, slug, "FROTA");
        Guid id = (await AtribuirAsync(tenant, new NovaAtribuicao(frota, Guid.NewGuid(), Translado(), DateTime.UtcNow.AddMinutes(-1)))).Value;
        HttpClient terceiro = Terceiro(tenant, slug, frota, TiposTerceiro.Frota);

        JsonArray expiradas = (await terceiro.GetFromJsonAsync<JsonArray>("/api/v1/terceiros/atribuicoes?status=EXPIRADA", Json, Ct))!;
        HttpResponseMessage aceite = await terceiro.PostAsync($"/api/v1/terceiros/atribuicoes/{id}/aceite", null, Ct);

        Assert.Single(expiradas);
        Assert.Empty((await terceiro.GetFromJsonAsync<JsonArray>("/api/v1/terceiros/atribuicoes?status=PENDENTE", Json, Ct))!);
        Assert.Equal(HttpStatusCode.Conflict, aceite.StatusCode);
    }

    [Fact]
    public async Task Atribuicao_exige_tipo_certo_terceiro_ativo_e_nao_duplica()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        Guid frota = await CadastrarAsync(tenant, slug, "FROTA");
        Guid origem = Guid.NewGuid();

        Result<Guid> pedidoParaFrota = await AtribuirAsync(tenant, new NovaAtribuicao(frota, Guid.NewGuid(), Pedido()));
        Result<Guid> primeira = await AtribuirAsync(tenant, new NovaAtribuicao(frota, origem, Translado()));
        Result<Guid> repetida = await AtribuirAsync(tenant, new NovaAtribuicao(frota, origem, Translado()));

        Assert.Equal("Atribuicao.TipoIncompativel", pedidoParaFrota.Error.Code);
        Assert.Equal(primeira.Value, repetida.Value);

        await Gestor(tenant, slug).PatchAsJsonAsync($"/api/v1/gestao/terceiros/{frota}/status", new { ativo = false }, Json, Ct);
        Result<Guid> inativo = await AtribuirAsync(tenant, new NovaAtribuicao(frota, Guid.NewGuid(), Translado()));
        Assert.Equal("Atribuicao.TerceiroIndisponivel", inativo.Error.Code);
    }

    [Fact]
    public async Task Portal_mostra_o_proprio_cadastro_e_e_so_de_terceiros()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        Guid frota = await CadastrarAsync(tenant, slug, "FROTA");

        JsonObject me = (await Terceiro(tenant, slug, frota, TiposTerceiro.Frota).GetFromJsonAsync<JsonObject>("/api/v1/terceiros/me", Json, Ct))!;

        Assert.Equal(frota, me["id_terceiro"]!.GetValue<Guid>());
        Assert.Equal(HttpStatusCode.Forbidden, (await Gestor(tenant, slug).GetAsync("/api/v1/terceiros/atribuicoes", Ct)).StatusCode);
    }

    private async Task<Result<Guid>> AtribuirAsync(Guid tenant, NovaAtribuicao atribuicao)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        return await scope.ServiceProvider.GetRequiredService<ITerceirosAtribuicoes>().AtribuirAsync(atribuicao, Ct);
    }

    private async Task<Guid> CadastrarAsync(Guid tenant, string slug, string tipo)
    {
        HttpResponseMessage response = await Gestor(tenant, slug).PostAsJsonAsync("/api/v1/gestao/terceiros", new
        {
            tipo,
            email = $"parceiro-{Guid.NewGuid():N}@teste.localhost",
            nome_razao_social = "Parceiro Ltda",
            cpf_cnpj = DocumentosDeTeste.NovoCnpj()
        }, Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_terceiro"]!.GetValue<Guid>();
    }

    private HttpClient Gestor(Guid tenant, string slug) => Cliente(slug, TokenDeTeste.Gerar(Perfis.Funeraria, tenant));

    private HttpClient Terceiro(Guid tenant, string slug, Guid idTerceiro, string tipo) =>
        Cliente(slug, TokenDeTeste.GerarTerceiro(tenant, idTerceiro, tipo));

    private HttpClient Cliente(string slug, string token)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static ResumoTranslado Translado() => new(
        "José Lima", "Hospital Central", "Cemitério da Paz", DateTime.UtcNow.AddHours(5),
        new ContatoResponsavel("Ana Lima", "(11) 98888-7777"), [new DocumentoAtribuicao("Declaração de óbito", Guid.NewGuid())]);

    private static ResumoPedido Pedido() => new(
        "José Lima", "Sala 2 - Velório Municipal", DateTime.UtcNow.AddHours(8), [new ItemPedido("Coroa de flores", 2, "Faixa: saudades")]);
}
