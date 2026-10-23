using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Testing;

namespace Funeraria.Modules.Catalogo.IntegrationTests;

public sealed class CatalogoApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    protected override bool UsaArmazenamento => true;

    public ValueTask InitializeAsync() => IniciarAsync();
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<CatalogoApiFactory>
{
    public const string Nome = "Api";
}

/// <summary>FUN-01: catálogo de produtos e serviços da funerária.</summary>
[Collection(ApiCollection.Nome)]
public sealed class CatalogoTests(CatalogoApiFactory factory)
{
    /// <summary>PNG de 1x1 pixel.</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Funeraria_nova_recebe_o_catalogo_modelo_com_itens_inativos()
    {
        string slug = $"cat-{Guid.NewGuid():N}"[..20];
        HttpResponseMessage cadastro = await Http().PostAsJsonAsync("/api/v1/onboarding/funerarias", new
        {
            nome_fantasia = "Funerária Catálogo",
            razao_social = "Funerária Catálogo Ltda",
            cnpj = DocumentosDeTeste.NovoCnpj(),
            slug,
            email_contato = $"contato@{slug}.localhost",
            gestor = new { nome = "Gestora", email = $"gestora@{slug}.localhost" }
        }, Json, Ct);
        Assert.Equal(HttpStatusCode.Created, cadastro.StatusCode);
        Guid tenant = (await cadastro.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_tenant"]!.GetValue<Guid>();
        HttpClient gestor = Equipe(tenant, slug);

        // A cópia roda pelo outbox, em background.
        JsonArray categorias = [];
        for (int i = 0; i < 50 && categorias.Count == 0; i++)
        {
            await Task.Delay(200, Ct);
            categorias = (await gestor.GetFromJsonAsync<JsonArray>("/api/v1/gestao/catalogo/categorias", Json, Ct))!;
        }

        Assert.Equal(
            ["Urna", "Flores", "Velório", "Cerimônia", "Tanatopraxia", "Sepultamento", "Cremação", "Transmissão online", "Alimentação", "Outros"],
            categorias.Select(c => c!["nome"]!.GetValue<string>()));
        JsonArray itens = (await gestor.GetFromJsonAsync<JsonArray>("/api/v1/gestao/catalogo", Json, Ct))!;
        Assert.Equal(10, itens.Count);
        Assert.All(itens, i => Assert.False(i!["ativo"]!.GetValue<bool>()));
        Assert.Empty((await Http(slug).GetFromJsonAsync<JsonArray>("/api/v1/catalogo", Json, Ct))!);
    }

    [Fact]
    public async Task Gestor_monta_o_catalogo_e_o_cliente_ve_so_o_que_esta_ativo()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Equipe(tenant, slug);
        Guid urnas = await CriarCategoriaAsync(gestor, "Urna");
        JsonObject item = await CriarItemAsync(gestor, urnas, "Urna sextavada", 1500m, controlaEstoque: true, quantidade: 3);
        Guid idItem = item["id_item"]!.GetValue<Guid>();

        JsonObject comVariacao = await (await gestor.PostAsJsonAsync($"/api/v1/gestao/catalogo/{idItem}/variacoes",
            new { nome = "Mogno", acrescimo_preco = 1200m }, Json, Ct)).Content.ReadFromJsonAsync<JsonObject>(Json, Ct) ?? [];
        Guid idVariacao = Assert.Single(comVariacao["variacoes"]!.AsArray())!["id_variacao"]!.GetValue<Guid>();

        HttpClient publico = Http(slug);
        JsonNode visto = Assert.Single((await publico.GetFromJsonAsync<JsonArray>($"/api/v1/catalogo?categoria={urnas}", Json, Ct))!)!;
        Assert.Equal("Urna sextavada", visto["nome"]!.GetValue<string>());
        Assert.Null(visto["quantidade_estoque"]);
        Assert.Equal("Mogno", Assert.Single(visto["variacoes"]!.AsArray())!["nome"]!.GetValue<string>());

        // Variação desativada some para o cliente; o gestor continua vendo.
        await gestor.PatchAsJsonAsync($"/api/v1/gestao/catalogo/{idItem}/variacoes/{idVariacao}/status", new { ativo = false }, Json, Ct);
        JsonObject detalhe = (await publico.GetFromJsonAsync<JsonObject>($"/api/v1/catalogo/{idItem}", Json, Ct))!;
        Assert.Empty(detalhe["variacoes"]!.AsArray());

        // Item desativado e categoria desativada somem do catálogo do cliente.
        Assert.Equal(HttpStatusCode.NoContent, (await gestor.PatchAsJsonAsync($"/api/v1/gestao/catalogo/{idItem}/status", new { ativo = false }, Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await publico.GetAsync($"/api/v1/catalogo/{idItem}", Ct)).StatusCode);
        await gestor.PatchAsJsonAsync($"/api/v1/gestao/catalogo/{idItem}/status", new { ativo = true }, Json, Ct);
        await gestor.PatchAsJsonAsync($"/api/v1/gestao/catalogo/categorias/{urnas}/status", new { ativo = false }, Json, Ct);
        Assert.Empty((await publico.GetFromJsonAsync<JsonArray>("/api/v1/catalogo", Json, Ct))!);
        Assert.Empty((await publico.GetFromJsonAsync<JsonArray>("/api/v1/catalogo/categorias", Json, Ct))!);
        Assert.Single((await gestor.GetFromJsonAsync<JsonArray>("/api/v1/gestao/catalogo", Json, Ct))!);
    }

    [Fact]
    public async Task Gestor_altera_o_item()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Equipe(tenant, slug);
        Guid flores = await CriarCategoriaAsync(gestor, "Flores");
        Guid idItem = (await CriarItemAsync(gestor, flores, "Coroa", 250m))["id_item"]!.GetValue<Guid>();

        HttpResponseMessage response = await gestor.PutAsJsonAsync($"/api/v1/gestao/catalogo/{idItem}",
            new { id_categoria = flores, nome = "Coroa de rosas", descricao = "Com faixa", preco = 320m }, Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonObject item = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("Coroa de rosas", item["nome"]!.GetValue<string>());
        Assert.Equal(320m, item["preco"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Nomes_repetidos_sao_recusados()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Equipe(tenant, slug);
        Guid urnas = await CriarCategoriaAsync(gestor, "Urna");
        Guid idItem = (await CriarItemAsync(gestor, urnas, "Urna padrão", 1000m))["id_item"]!.GetValue<Guid>();
        await gestor.PostAsJsonAsync($"/api/v1/gestao/catalogo/{idItem}/variacoes", new { nome = "Pinus", acrescimo_preco = -200m }, Json, Ct);

        HttpResponseMessage categoria = await gestor.PostAsJsonAsync("/api/v1/gestao/catalogo/categorias", new { nome = "urna" }, Json, Ct);
        HttpResponseMessage item = await gestor.PostAsJsonAsync("/api/v1/gestao/catalogo", new { id_categoria = urnas, nome = "URNA PADRÃO", preco = 1m }, Json, Ct);
        HttpResponseMessage variacao = await gestor.PostAsJsonAsync($"/api/v1/gestao/catalogo/{idItem}/variacoes", new { nome = "pinus", acrescimo_preco = 0m }, Json, Ct);

        Assert.Equal("Já existe uma categoria com este nome.", await DetalheAsync(categoria, HttpStatusCode.Conflict));
        Assert.Equal("Já existe um item com este nome nesta categoria.", await DetalheAsync(item, HttpStatusCode.Conflict));
        Assert.Equal("Já existe uma variação com este nome neste item.", await DetalheAsync(variacao, HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Dados_invalidos_retornam_400()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Equipe(tenant, slug);
        Guid urnas = await CriarCategoriaAsync(gestor, "Urna");
        Guid idItem = (await CriarItemAsync(gestor, urnas, "Urna padrão", 1000m))["id_item"]!.GetValue<Guid>();

        HttpResponseMessage vazio = await gestor.PostAsJsonAsync("/api/v1/gestao/catalogo", new { controla_estoque = true }, Json, Ct);
        JsonObject erros = (await vazio.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["errors"]!.AsObject();
        Assert.Equal(HttpStatusCode.BadRequest, vazio.StatusCode);
        Assert.Equal("Categoria é obrigatória.", erros["id_categoria"]![0]!.GetValue<string>());
        Assert.Equal("Nome do item é obrigatório.", erros["nome"]![0]!.GetValue<string>());
        Assert.Equal("Preço é obrigatório.", erros["preco"]![0]!.GetValue<string>());
        Assert.Equal("Informe a quantidade em estoque.", erros["quantidade_estoque"]![0]!.GetValue<string>());

        HttpResponseMessage semCategoria = await gestor.PostAsJsonAsync("/api/v1/gestao/catalogo", new { id_categoria = Guid.NewGuid(), nome = "X", preco = 1m }, Json, Ct);
        Assert.Equal("Categoria não encontrada.", await DetalheAsync(semCategoria, HttpStatusCode.NotFound));

        HttpResponseMessage negativa = await gestor.PostAsJsonAsync($"/api/v1/gestao/catalogo/{idItem}/variacoes", new { nome = "Brinde", acrescimo_preco = -1001m }, Json, Ct);
        Assert.Equal("O desconto da variação não pode deixar o preço abaixo de zero.", await DetalheAsync(negativa, HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Imagem_do_item_fica_publica_pela_api_e_so_aceita_imagem()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Equipe(tenant, slug);
        Guid flores = await CriarCategoriaAsync(gestor, "Flores");
        Guid idItem = (await CriarItemAsync(gestor, flores, "Coroa", 250m))["id_item"]!.GetValue<Guid>();

        HttpResponseMessage enviada = await EnviarImagemAsync(gestor, idItem, Png);
        Assert.Equal(HttpStatusCode.OK, enviada.StatusCode);
        string url = (await enviada.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["imagem_url"]!.GetValue<string>();
        Assert.StartsWith($"http://api.teste.localhost/api/v1/funerarias/{slug}/catalogo/{idItem}/imagem?v=", url, StringComparison.Ordinal);

        HttpResponseMessage imagem = await Http().GetAsync(new Uri(url).PathAndQuery, Ct);
        Assert.Equal(HttpStatusCode.OK, imagem.StatusCode);
        Assert.Equal(Png, await imagem.Content.ReadAsByteArrayAsync(Ct));

        HttpResponseMessage texto = await EnviarImagemAsync(gestor, idItem, Encoding.UTF8.GetBytes("<svg/>"));
        Assert.Equal(HttpStatusCode.BadRequest, texto.StatusCode);
    }

    [Fact]
    public async Task Item_de_outra_funeraria_nao_aparece()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (Guid outra, string slugOutra) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Equipe(tenant, slug);
        Guid idItem = (await CriarItemAsync(gestor, await CriarCategoriaAsync(gestor, "Urna"), "Urna", 1m))["id_item"]!.GetValue<Guid>();

        Assert.Equal(HttpStatusCode.NotFound, (await Http(slugOutra).GetAsync($"/api/v1/catalogo/{idItem}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Equipe(outra, slugOutra).PatchAsJsonAsync($"/api/v1/gestao/catalogo/{idItem}/status", new { ativo = false }, Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task So_o_gestor_altera_o_catalogo()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();

        HttpResponseMessage funcionario = await Equipe(tenant, slug, Perfis.Funcionario)
            .PostAsJsonAsync("/api/v1/gestao/catalogo/categorias", new { nome = "Urna" }, Json, Ct);
        HttpResponseMessage consultor = await Equipe(tenant, slug, Perfis.Consultor)
            .PostAsJsonAsync("/api/v1/gestao/catalogo/categorias", new { nome = "Urna" }, Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, funcionario.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, consultor.StatusCode);
    }

    private HttpClient Http(string? slug = null)
    {
        HttpClient client = factory.CreateClient();
        if (slug is not null)
        {
            client.DefaultRequestHeaders.Add("X-Tenant", slug);
        }

        return client;
    }

    private HttpClient Equipe(Guid tenant, string slug, string perfil = Perfis.Funeraria)
    {
        HttpClient client = Http(slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(perfil, tenant));
        return client;
    }

    private static async Task<Guid> CriarCategoriaAsync(HttpClient gestor, string nome)
    {
        HttpResponseMessage response = await gestor.PostAsJsonAsync("/api/v1/gestao/catalogo/categorias", new { nome }, Json, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_categoria"]!.GetValue<Guid>();
    }

    private static async Task<JsonObject> CriarItemAsync(
        HttpClient gestor, Guid categoria, string nome, decimal preco, bool controlaEstoque = false, int? quantidade = null)
    {
        HttpResponseMessage response = await gestor.PostAsJsonAsync("/api/v1/gestao/catalogo", new
        {
            id_categoria = categoria,
            nome,
            preco,
            controla_estoque = controlaEstoque,
            quantidade_estoque = quantidade
        }, Json, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
    }

    private static async Task<HttpResponseMessage> EnviarImagemAsync(HttpClient gestor, Guid idItem, byte[] conteudo)
    {
        using var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(arquivo, "arquivo", "imagem.png");
        return await gestor.PostAsync($"/api/v1/gestao/catalogo/{idItem}/imagem", form, Ct);
    }

    private static async Task<string> DetalheAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.True(response.StatusCode == status, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["detail"]!.GetValue<string>();
    }
}
