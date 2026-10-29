using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.Testing;

namespace Funeraria.Modules.Funeral.IntegrationTests;

/// <summary>FUN-03: itens do funeral, coberto x adicional, estoque e importação das preferências.</summary>
[Collection(ApiCollection.Nome)]
public sealed class ItensFuneralTests(FuneralApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Separa_coberto_de_adicional_e_controla_o_estoque()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        HttpClient gestor = funeraria.Equipe();
        (Guid urna, Guid mogno) = await UrnaComEstoqueAsync(gestor, estoque: 2);
        Guid coroa = await funeraria.CriarItemCatalogoAsync("Coroa de flores", 300m);
        Guid plano = await funeraria.CriarPlanoAsync(carenciaDias: 0);
        await gestor.PutAsJsonAsync($"/api/v1/gestao/planos/{plano}/coberturas",
            new { coberturas = new[] { new { id_item_catalogo = urna, quantidade = 1 } } }, Json, Ct);
        FuneralDeTeste funeral = await factory.CriarFuneralAsync(funeraria, plano);

        // Urna coberta, mas em mogno: a família paga só a diferença da variação.
        JsonObject composicao = await AdicionarAsync(gestor, funeral.IdFuneral, urna, mogno, 1);
        Assert.Equal("Urna padrão", Assert.Single(composicao["itens_cobertos"]!.AsArray())!["nome"]!.GetValue<string>());
        JsonNode diferenca = Assert.Single(composicao["itens_adicionais"]!.AsArray())!;
        Assert.True(diferenca["somente_diferenca_variacao"]!.GetValue<bool>());
        Assert.Equal(500m, composicao["total_adicional"]!.GetValue<decimal>());

        composicao = await AdicionarAsync(gestor, funeral.IdFuneral, coroa, null, 2);
        Assert.Equal(1100m, composicao["total_adicional"]!.GetValue<decimal>());

        // Sobrou 1 urna no estoque: pedir 2 é recusado; a segunda urna passa da cobertura e é adicional inteira.
        HttpResponseMessage semEstoque = await gestor.PostAsJsonAsync($"/api/v1/funerais/{funeral.IdFuneral}/itens",
            new { id_item_catalogo = urna, quantidade = 2 }, Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, semEstoque.StatusCode);
        composicao = await AdicionarAsync(gestor, funeral.IdFuneral, urna, null, 1);
        Assert.Equal(2100m, composicao["total_adicional"]!.GetValue<decimal>());
        Assert.Equal(0, await EstoqueAsync(gestor, urna));

        Guid segundaUrna = composicao["itens_adicionais"]!.AsArray()
            .Last(i => i!["id_item_catalogo"]!.GetValue<Guid>() == urna)!["id_funeral_item"]!.GetValue<Guid>();
        HttpResponseMessage removido = await gestor.DeleteAsync($"/api/v1/funerais/{funeral.IdFuneral}/itens/{segundaUrna}", Ct);
        Assert.Equal(1100m, (await Cenarios.LerAsync(removido, HttpStatusCode.OK))["total_adicional"]!.GetValue<decimal>());
        Assert.Equal(1, await EstoqueAsync(gestor, urna));

        // O titular acompanha os itens; o funeral cancelado devolve o estoque e não aceita mais itens.
        Assert.Equal(HttpStatusCode.OK, (await funeral.Cliente.Http.GetAsync($"/api/v1/funerais/{funeral.IdFuneral}/itens", Ct)).StatusCode);
        await gestor.PatchAsJsonAsync($"/api/v1/funerais/{funeral.IdFuneral}/status", new { status = "CANCELADO" }, Json, Ct);
        for (int i = 0; i < 75 && await EstoqueAsync(gestor, urna) != 2; i++)
        {
            await Task.Delay(200, Ct);
        }

        Assert.Equal(2, await EstoqueAsync(gestor, urna));
        Assert.Equal(HttpStatusCode.Conflict, (await gestor.PostAsJsonAsync($"/api/v1/funerais/{funeral.IdFuneral}/itens",
            new { id_item_catalogo = coroa, quantidade = 1 }, Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Importa_as_preferencias_registradas_em_vida()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        HttpClient gestor = funeraria.Equipe();
        Guid coroa = await funeraria.CriarItemCatalogoAsync("Coroa de flores", 300m);
        Guid velas = await funeraria.CriarItemCatalogoAsync("Velas", 40m);
        FuneralDeTeste funeral = await factory.CriarFuneralAsync(funeraria);

        Assert.Equal(HttpStatusCode.NotFound, (await ImportarAsync(gestor, funeral.IdFuneral)).StatusCode);

        HttpResponseMessage preferencia = await funeral.Cliente.Http.PutAsJsonAsync($"/api/v1/preferencias-funeral/{funeral.Cliente.IdCliente}", new
        {
            destinacao = "SEPULTAMENTO",
            tipo_cerimonia = "CIVIL",
            itens = new object[] { new { id_item_catalogo = coroa, quantidade = 1 }, new { id_item_catalogo = velas, quantidade = 4 } }
        }, Json, Ct);
        Assert.True(preferencia.IsSuccessStatusCode, await preferencia.Content.ReadAsStringAsync(Ct));

        // As velas saem do catálogo antes do funeral: ficam de fora, com o motivo.
        await gestor.PatchAsJsonAsync($"/api/v1/gestao/catalogo/{velas}/status", new { ativo = false }, Json, Ct);
        JsonObject importacao = await Cenarios.LerAsync(await ImportarAsync(gestor, funeral.IdFuneral), HttpStatusCode.OK);
        Assert.Equal(1, importacao["importados"]!.GetValue<int>());
        Assert.Equal(velas, Assert.Single(importacao["ignorados"]!.AsArray())!["id_item_catalogo"]!.GetValue<Guid>());
        Assert.Equal(300m, importacao["composicao"]!["total_adicional"]!.GetValue<decimal>());

        // Importar de novo não duplica.
        JsonObject deNovo = await Cenarios.LerAsync(await ImportarAsync(gestor, funeral.IdFuneral), HttpStatusCode.OK);
        Assert.Equal(0, deNovo["importados"]!.GetValue<int>());
        Assert.Equal(300m, deNovo["composicao"]!["total_adicional"]!.GetValue<decimal>());
    }

    private static async Task<(Guid Urna, Guid Mogno)> UrnaComEstoqueAsync(HttpClient gestor, int estoque)
    {
        HttpResponseMessage categoria = await gestor.PostAsJsonAsync("/api/v1/gestao/catalogo/categorias", new { nome = $"Urnas {Guid.NewGuid():N}"[..14] }, Json, Ct);
        Guid idCategoria = (await Cenarios.LerAsync(categoria, HttpStatusCode.Created))["id_categoria"]!.GetValue<Guid>();
        HttpResponseMessage item = await gestor.PostAsJsonAsync("/api/v1/gestao/catalogo", new
        {
            id_categoria = idCategoria, nome = "Urna padrão", preco = 1000m, controla_estoque = true, quantidade_estoque = estoque
        }, Json, Ct);
        Guid urna = (await Cenarios.LerAsync(item, HttpStatusCode.Created))["id_item"]!.GetValue<Guid>();
        HttpResponseMessage variacao = await gestor.PostAsJsonAsync($"/api/v1/gestao/catalogo/{urna}/variacoes", new { nome = "Mogno", acrescimo_preco = 500m }, Json, Ct);
        JsonObject comVariacao = await Cenarios.LerAsync(variacao, HttpStatusCode.OK);
        return (urna, comVariacao["variacoes"]![0]!["id_variacao"]!.GetValue<Guid>());
    }

    private static async Task<JsonObject> AdicionarAsync(HttpClient gestor, Guid idFuneral, Guid item, Guid? variacao, int quantidade) =>
        await Cenarios.LerAsync(
            await gestor.PostAsJsonAsync($"/api/v1/funerais/{idFuneral}/itens", new { id_item_catalogo = item, id_variacao = variacao, quantidade }, Json, Ct),
            HttpStatusCode.OK);

    private static Task<HttpResponseMessage> ImportarAsync(HttpClient gestor, Guid idFuneral) =>
        gestor.PostAsync($"/api/v1/funerais/{idFuneral}/itens/importar-preferencias", null, Ct);

    private static async Task<int> EstoqueAsync(HttpClient gestor, Guid item) =>
        (await gestor.GetFromJsonAsync<JsonArray>("/api/v1/gestao/catalogo", Json, Ct))!
            .Single(i => i!["id_item"]!.GetValue<Guid>() == item)!["quantidade_estoque"]!.GetValue<int>();
}
