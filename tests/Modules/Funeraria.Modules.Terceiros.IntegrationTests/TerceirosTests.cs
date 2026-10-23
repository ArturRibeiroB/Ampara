using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Terceiros.Contracts.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;
using Funeraria.Modules.Notificacoes.Application.Email;
using Funeraria.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Funeraria.Modules.Terceiros.IntegrationTests;

public sealed class TerceirosApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public ValueTask InitializeAsync() => IniciarAsync();

    protected override void ConfigurarServicos(IServiceCollection services)
    {
        services.AddScoped<IIntegrationEventHandler<AtribuicaoAceitaIntegrationEvent>, CapturarEvento<AtribuicaoAceitaIntegrationEvent>>();
        services.AddScoped<IIntegrationEventHandler<AtribuicaoRecusadaIntegrationEvent>, CapturarEvento<AtribuicaoRecusadaIntegrationEvent>>();
    }
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<TerceirosApiFactory>
{
    public const string Nome = "Api";
}

/// <summary>TER-01: cadastro de terceiros pela funerária, com convite e acesso pela Identidade.</summary>
[Collection(ApiCollection.Nome)]
public sealed class TerceirosTests(TerceirosApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Terceiro_novo_recebe_convite_define_a_senha_fica_ativo_e_entra_como_terceiro()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        string email = NovoEmail();

        HttpResponseMessage criado = await Gestor(tenant, slug).PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(email, "FROTA"), Json, Ct);

        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        JsonObject terceiro = (await criado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Guid id = terceiro["id_terceiro"]!.GetValue<Guid>();
        Assert.Equal("CONVIDADO", terceiro["status"]!.GetValue<string>());
        Assert.Equal("SP", terceiro["area_atuacao"]!["ufs"]![0]!.GetValue<string>());

        EmailMensagem convite = await factory.Emails.AguardarAsync(email, e => e.CorpoTexto.Contains("definir-senha", StringComparison.Ordinal));
        Assert.Contains("parceiro(a)", convite.CorpoTexto, StringComparison.Ordinal);
        HttpResponseMessage definida = await Http(slug).PostAsJsonAsync("/api/v1/auth/definir-senha",
            new { token = EmailsCapturados.TokenDoLink(convite), senha = FunerariaApiFactory.SenhaPadrao }, Json, Ct);
        Assert.Equal(HttpStatusCode.NoContent, definida.StatusCode);

        await AguardarStatusAsync(tenant, slug, id, "ATIVO");

        HttpResponseMessage login = await Http(slug).PostAsJsonAsync("/api/v1/auth/login", new { email, senha = FunerariaApiFactory.SenhaPadrao }, Json, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var jwt = new JsonWebToken((await login.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["access_token"]!.GetValue<string>());
        Assert.Equal(Perfis.Terceiro, jwt.GetClaim("role").Value);
        Assert.Equal(TiposTerceiro.Frota, jwt.GetClaim("tipo_terceiro").Value);
        Assert.Equal(id.ToString(), jwt.GetClaim("id_terceiro").Value);
    }

    [Fact]
    public async Task Quem_ja_tem_conta_fica_ativo_sem_convite_de_senha()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string outra) = await factory.CriarFunerariaAsync();
        string email = NovoEmail();
        await CadastrarClienteAsync(outra, email);

        HttpResponseMessage criado = await Gestor(tenant, slug).PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(email, "FORNECEDOR"), Json, Ct);
        Guid id = (await criado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_terceiro"]!.GetValue<Guid>();

        await AguardarStatusAsync(tenant, slug, id, "ATIVO");
        await factory.Emails.AguardarAsync(email, e => e.Assunto.Contains("Acesso liberado", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Desativar_tira_o_acesso_a_esta_funeraria_e_reativar_devolve()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string outra) = await factory.CriarFunerariaAsync();
        string email = NovoEmail();
        await CadastrarClienteAsync(outra, email);
        HttpClient gestor = Gestor(tenant, slug);
        Guid id = (await (await gestor.PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(email, "CARTORIO_DESPACHANTE"), Json, Ct))
            .Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_terceiro"]!.GetValue<Guid>();
        await AguardarStatusAsync(tenant, slug, id, "ATIVO");

        Assert.Equal(HttpStatusCode.NoContent, (await gestor.PatchAsJsonAsync($"/api/v1/gestao/terceiros/{id}/status", new { ativo = false }, Json, Ct)).StatusCode);
        await AguardarStatusAsync(tenant, slug, id, "INATIVO");
        await AguardarLoginAsync(slug, email, HttpStatusCode.Forbidden);

        // A conta continua valendo na outra funerária.
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(outra, email)).StatusCode);

        await gestor.PatchAsJsonAsync($"/api/v1/gestao/terceiros/{id}/status", new { ativo = true }, Json, Ct);
        await AguardarStatusAsync(tenant, slug, id, "ATIVO");
        await AguardarLoginAsync(slug, email, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cpf_cnpj_e_email_repetidos_e_email_de_cliente_sao_recusados()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Gestor(tenant, slug);
        string cnpj = DocumentosDeTeste.NovoCnpj();
        string email = NovoEmail();
        await gestor.PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(email, "FROTA", cnpj), Json, Ct);
        string emailCliente = NovoEmail();
        await CadastrarClienteAsync(slug, emailCliente);

        Assert.Equal("CPF ou CNPJ já cadastrado nesta funerária.",
            await DetalheAsync(await gestor.PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(NovoEmail(), "FROTA", cnpj), Json, Ct)));
        Assert.Equal("Já existe um terceiro com este e-mail nesta funerária.",
            await DetalheAsync(await gestor.PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(email, "FROTA"), Json, Ct)));
        Assert.Equal("Este e-mail já tem acesso a esta funerária com outro perfil.",
            await DetalheAsync(await gestor.PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(emailCliente, "FROTA"), Json, Ct)));
    }

    [Fact]
    public async Task Fornecedor_informa_categorias_do_catalogo_e_os_outros_tipos_nao()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Gestor(tenant, slug);
        HttpResponseMessage categoria = await gestor.PostAsJsonAsync("/api/v1/gestao/catalogo/categorias", new { nome = "Flores" }, Json, Ct);
        Guid flores = (await categoria.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_categoria"]!.GetValue<Guid>();

        HttpResponseMessage fornecedor = await gestor.PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(NovoEmail(), "FORNECEDOR", categorias: [flores]), Json, Ct);
        HttpResponseMessage inexistente = await gestor.PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(NovoEmail(), "FORNECEDOR", categorias: [Guid.NewGuid()]), Json, Ct);
        HttpResponseMessage frota = await gestor.PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(NovoEmail(), "FROTA", categorias: [flores]), Json, Ct);

        Assert.Equal(HttpStatusCode.Created, fornecedor.StatusCode);
        Assert.Equal(flores, (await fornecedor.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["categorias_atendidas"]![0]!.GetValue<Guid>());
        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, frota.StatusCode);
    }

    [Fact]
    public async Task Dados_invalidos_retornam_400_com_as_mensagens()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();

        HttpResponseMessage response = await Gestor(tenant, slug).PostAsJsonAsync("/api/v1/gestao/terceiros",
            new { cpf_cnpj = "123", email = "x@", area_atuacao = new { ufs = new[] { "XX" } } }, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonObject erros = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["errors"]!.AsObject();
        Assert.Equal("Tipo é obrigatório. Use FROTA, CARTORIO_DESPACHANTE ou FORNECEDOR.", erros["tipo"]![0]!.GetValue<string>());
        Assert.Equal("Nome ou razão social é obrigatório.", erros["nome_razao_social"]![0]!.GetValue<string>());
        Assert.Equal("CPF ou CNPJ não informado ou em formato inválido.", erros["cpf_cnpj"]![0]!.GetValue<string>());
        Assert.Equal("E-mail inválido.", erros["email"]![0]!.GetValue<string>());
        Assert.Equal("UF inválida na área de atuação.", erros["area_atuacao.ufs"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Lista_por_tipo_e_status_e_altera_os_dados()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Gestor(tenant, slug);
        await gestor.PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(NovoEmail(), "FROTA"), Json, Ct);
        HttpResponseMessage criado = await gestor.PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(NovoEmail(), "CARTORIO_DESPACHANTE"), Json, Ct);
        Guid despachante = (await criado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_terceiro"]!.GetValue<Guid>();

        JsonObject lista = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/terceiros?tipo=CARTORIO_DESPACHANTE", Json, Ct))!;
        Assert.Equal(despachante, Assert.Single(lista["itens"]!.AsArray())!["id_terceiro"]!.GetValue<Guid>());
        Assert.Equal(HttpStatusCode.BadRequest, (await gestor.GetAsync("/api/v1/gestao/terceiros?tipo=CARRO", Ct)).StatusCode);

        HttpResponseMessage alterado = await gestor.PutAsJsonAsync($"/api/v1/gestao/terceiros/{despachante}", new
        {
            nome_razao_social = "Despachante Central",
            cpf_cnpj = DocumentosDeTeste.NovoCnpj(),
            area_atuacao = new { cidades = new[] { new { nome = "Maringá", uf = "pr" } } }
        }, Json, Ct);
        Assert.Equal(HttpStatusCode.OK, alterado.StatusCode);
        JsonObject depois = (await alterado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("Despachante Central", depois["nome_razao_social"]!.GetValue<string>());
        Assert.Equal("PR", depois["area_atuacao"]!["cidades"]![0]!["uf"]!.GetValue<string>());
    }

    [Fact]
    public async Task So_o_gestor_cadastra_e_cada_funeraria_ve_os_seus()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (Guid outra, string slugOutra) = await factory.CriarFunerariaAsync();
        HttpResponseMessage criado = await Gestor(tenant, slug).PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(NovoEmail(), "FROTA"), Json, Ct);
        Guid id = (await criado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_terceiro"]!.GetValue<Guid>();

        HttpClient funcionario = Http(slug);
        funcionario.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(Perfis.Funcionario, tenant));

        Assert.Equal(HttpStatusCode.Forbidden,
            (await funcionario.PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(NovoEmail(), "FROTA"), Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Gestor(outra, slugOutra).GetAsync($"/api/v1/gestao/terceiros/{id}", Ct)).StatusCode);
    }

    private async Task AguardarStatusAsync(Guid tenant, string slug, Guid id, string status)
    {
        string atual = string.Empty;
        for (int i = 0; i < 75 && atual != status; i++)
        {
            await Task.Delay(200, Ct);
            atual = (await Gestor(tenant, slug).GetFromJsonAsync<JsonObject>($"/api/v1/gestao/terceiros/{id}", Json, Ct))!["status"]!.GetValue<string>();
        }

        Assert.Equal(status, atual);
    }

    private async Task AguardarLoginAsync(string slug, string email, HttpStatusCode esperado)
    {
        HttpStatusCode atual = default;
        for (int i = 0; i < 75 && atual != esperado; i++)
        {
            await Task.Delay(200, Ct);
            atual = (await LoginAsync(slug, email)).StatusCode;
        }

        Assert.Equal(esperado, atual);
    }

    private Task<HttpResponseMessage> LoginAsync(string slug, string email) =>
        Http(slug).PostAsJsonAsync("/api/v1/auth/login", new { email, senha = FunerariaApiFactory.SenhaPadrao }, Json, Ct);

    private async Task CadastrarClienteAsync(string slug, string email)
    {
        HttpResponseMessage response = await Http(slug).PostAsJsonAsync("/api/v1/clientes", new
        {
            nome = "Pessoa Já Cadastrada",
            cpf = DocumentosDeTeste.NovoCpf(),
            data_nascimento = "1980-01-01",
            email,
            senha = FunerariaApiFactory.SenhaPadrao
        }, Json, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
    }

    private HttpClient Http(string slug)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", slug);
        return client;
    }

    private HttpClient Gestor(Guid tenant, string slug)
    {
        HttpClient client = Http(slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(Perfis.Funeraria, tenant));
        return client;
    }

    private static async Task<string> DetalheAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["detail"]!.GetValue<string>();
    }

    private static string NovoEmail() => $"parceiro-{Guid.NewGuid():N}@teste.localhost";

    private static object NovoTerceiro(string email, string tipo, string? cpfCnpj = null, Guid[]? categorias = null) => new
    {
        tipo,
        email,
        nome_razao_social = "Parceiro Teste Ltda",
        cpf_cnpj = cpfCnpj ?? DocumentosDeTeste.NovoCnpj(),
        telefone = "(11) 3333-4444",
        area_atuacao = new { ufs = new[] { "sp" }, cidades = Array.Empty<object>() },
        categorias_atendidas = categorias
    };
}
