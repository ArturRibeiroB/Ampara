using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Testing;

namespace Funeraria.Modules.Planos.IntegrationTests;

/// <summary>PLA-02: itens do catálogo cobertos pelo plano.</summary>
[Collection(ApiCollection.Nome)]
public sealed class CoberturasTests(PlanosApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Gestor_define_as_coberturas_e_o_cliente_ve_no_detalhe_do_plano()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Http(slug, Perfis.Funeraria, tenant);
        Guid plano = await CriarPlanoAsync(gestor);
        Guid urna = await CriarItemAsync(gestor, "Urna", "Urna padrão");
        Guid coroa = await CriarItemAsync(gestor, "Flores", "Coroa de flores");

        HttpResponseMessage definidas = await gestor.PutAsJsonAsync($"/api/v1/gestao/planos/{plano}/coberturas", new
        {
            coberturas = new object[]
            {
                new { id_item_catalogo = urna, quantidade = 1 },
                new { id_item_catalogo = coroa, quantidade = 2 }
            }
        }, Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, definidas.StatusCode);

        JsonObject detalhe = (await Http(slug).GetFromJsonAsync<JsonObject>($"/api/v1/planos/{plano}", Json, Ct))!;
        JsonArray coberturas = detalhe["coberturas"]!.AsArray();
        Assert.Equal(["Flores", "Urna"], coberturas.Select(c => c!["categoria"]!.GetValue<string>()));
        JsonNode flores = coberturas[0]!;
        Assert.Equal("Coroa de flores", flores["nome"]!.GetValue<string>());
        Assert.Equal(2, flores["quantidade"]!.GetValue<int>());
        Assert.True(flores["item_ativo"]!.GetValue<bool>());

        // A lista é trocada inteira: o item que sai deixa de ser coberto, o que fica pode mudar de quantidade.
        await gestor.PutAsJsonAsync($"/api/v1/gestao/planos/{plano}/coberturas",
            new { coberturas = new object[] { new { id_item_catalogo = coroa, quantidade = 3 } } }, Json, Ct);
        JsonArray depois = (await Http(slug).GetFromJsonAsync<JsonObject>($"/api/v1/planos/{plano}", Json, Ct))!["coberturas"]!.AsArray();
        Assert.Equal(3, Assert.Single(depois)!["quantidade"]!.GetValue<int>());

        await gestor.PutAsJsonAsync($"/api/v1/gestao/planos/{plano}/coberturas", new { coberturas = Array.Empty<object>() }, Json, Ct);
        Assert.Empty((await Http(slug).GetFromJsonAsync<JsonObject>($"/api/v1/planos/{plano}", Json, Ct))!["coberturas"]!.AsArray());
    }

    [Fact]
    public async Task Item_inativo_ou_de_outra_funeraria_nao_entra_na_cobertura()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (Guid outra, string slugOutra) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Http(slug, Perfis.Funeraria, tenant);
        Guid plano = await CriarPlanoAsync(gestor);
        Guid inativo = await CriarItemAsync(gestor, "Urna", "Urna antiga");
        await gestor.PatchAsJsonAsync($"/api/v1/gestao/catalogo/{inativo}/status", new { ativo = false }, Json, Ct);
        Guid deOutra = await CriarItemAsync(Http(slugOutra, Perfis.Funeraria, outra), "Urna", "Urna");

        foreach (Guid item in new[] { inativo, deOutra })
        {
            HttpResponseMessage response = await gestor.PutAsJsonAsync($"/api/v1/gestao/planos/{plano}/coberturas",
                new { coberturas = new object[] { new { id_item_catalogo = item, quantidade = 1 } } }, Json, Ct);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            JsonObject problem = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
            Assert.Contains(item.ToString(), problem["errors"]!["coberturas"]![0]!.GetValue<string>(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Item_repetido_ou_quantidade_invalida_sao_recusados()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Http(slug, Perfis.Funeraria, tenant);
        Guid plano = await CriarPlanoAsync(gestor);
        Guid urna = await CriarItemAsync(gestor, "Urna", "Urna padrão");

        HttpResponseMessage repetido = await gestor.PutAsJsonAsync($"/api/v1/gestao/planos/{plano}/coberturas", new
        {
            coberturas = new object[] { new { id_item_catalogo = urna, quantidade = 1 }, new { id_item_catalogo = urna, quantidade = 2 } }
        }, Json, Ct);
        HttpResponseMessage zero = await gestor.PutAsJsonAsync($"/api/v1/gestao/planos/{plano}/coberturas",
            new { coberturas = new object[] { new { id_item_catalogo = urna, quantidade = 0 } } }, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        string erros = await zero.Content.ReadAsStringAsync(Ct);
        Assert.Contains("Quantidade deve ser pelo menos 1.", erros, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plano_inexistente_e_funcionario_sao_recusados()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        var corpo = new { coberturas = Array.Empty<object>() };

        HttpResponseMessage inexistente = await Http(slug, Perfis.Funeraria, tenant)
            .PutAsJsonAsync($"/api/v1/gestao/planos/{Guid.NewGuid()}/coberturas", corpo, Json, Ct);
        HttpResponseMessage funcionario = await Http(slug, Perfis.Funcionario, tenant)
            .PutAsJsonAsync($"/api/v1/gestao/planos/{Guid.NewGuid()}/coberturas", corpo, Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, funcionario.StatusCode);
    }

    private HttpClient Http(string slug, string? perfil = null, Guid? tenant = null)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", slug);
        if (perfil is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(perfil, tenant));
        }

        return client;
    }

    private static async Task<Guid> CriarPlanoAsync(HttpClient gestor)
    {
        HttpResponseMessage response = await gestor.PostAsJsonAsync("/api/v1/gestao/planos", new
        {
            nome = $"Plano {Guid.NewGuid():N}"[..20],
            valor_mensal = 89.90m,
            valor_anual = 970.92m,
            valor_dependente_mensal = 19.90m,
            valor_dependente_anual = 214.92m,
            carencia_dias = 90,
            limite_dependentes = 4,
            permite_somente_dependente = false
        }, Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_plano"]!.GetValue<Guid>();
    }

    private static async Task<Guid> CriarItemAsync(HttpClient gestor, string categoria, string nome)
    {
        HttpResponseMessage cat = await gestor.PostAsJsonAsync("/api/v1/gestao/catalogo/categorias", new { nome = categoria }, Json, Ct);
        Guid idCategoria = (await cat.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_categoria"]!.GetValue<Guid>();
        HttpResponseMessage item = await gestor.PostAsJsonAsync("/api/v1/gestao/catalogo", new { id_categoria = idCategoria, nome, preco = 100m }, Json, Ct);
        Assert.Equal(HttpStatusCode.Created, item.StatusCode);
        return (await item.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_item"]!.GetValue<Guid>();
    }
}
