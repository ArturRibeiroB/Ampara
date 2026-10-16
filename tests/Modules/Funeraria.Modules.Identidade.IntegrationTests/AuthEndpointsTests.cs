using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Funeraria.Modules.Identidade.IntegrationTests;

[Collection(ApiCollection.Nome)]
public sealed class AuthEndpointsTests(IdentidadeApiFactory factory)
{
    private const string Cookie = "funeraria_refresh";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_na_funeraria_emite_token_com_o_perfil_do_vinculo_e_refresh_em_cookie_seguro()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (Guid idUsuario, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funeraria));

        HttpResponseMessage response = await LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonObject body = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Null(body["refresh_token"]);
        Assert.Equal("FUNERARIA", body["usuario"]!["role"]!.GetValue<string>());
        Assert.Equal(idUsuario, body["usuario"]!["id"]!.GetValue<Guid>());

        var jwt = new JsonWebToken(body["access_token"]!.GetValue<string>());
        Assert.Equal(idUsuario.ToString(), jwt.Subject);
        Assert.Equal(tenant.ToString(), jwt.GetClaim("id_tenant").Value);
        Assert.Equal("FUNERARIA", jwt.GetClaim("role").Value);

        string setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(Cookie + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);

        // O token emitido abre as rotas do perfil.
        HttpClient client = Cliente(slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.EncodedToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/gestao/planos", Ct)).StatusCode);
    }

    [Fact]
    public async Task Senha_errada_e_email_inexistente_recebem_a_mesma_resposta()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));

        HttpResponseMessage senhaErrada = await LoginAsync(slug, email, "Errada@123");
        HttpResponseMessage emailInexistente = await LoginAsync(slug, "ninguem@teste.localhost", "Errada@123");

        foreach (HttpResponseMessage response in new[] { senhaErrada, emailInexistente })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            JsonObject problem = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
            Assert.Equal("Auth.CredenciaisInvalidas", problem["code"]!.GetValue<string>());
            Assert.Equal("E-mail ou senha inválidos.", problem["detail"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task Campos_vazios_retornam_400_com_as_mensagens_do_card()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();

        HttpResponseMessage response = await Cliente(slug).PostAsJsonAsync("/api/v1/auth/login", new { email = "", senha = "" }, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonObject erros = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["errors"]!.AsObject();
        Assert.Equal("E-mail é obrigatório.", erros["email"]![0]!.GetValue<string>());
        Assert.Equal("Senha é obrigatória.", erros["senha"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Usuario_sem_vinculo_com_a_funeraria_nao_entra()
    {
        (Guid tenantA, _) = await factory.CriarFunerariaAsync();
        (_, string slugB) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenantA, PerfilVinculo.Funcionario));

        HttpResponseMessage response = await LoginAsync(slugB, email, FunerariaApiFactory.SenhaPadrao);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Auth.SemAcessoFuneraria");
    }

    [Fact]
    public async Task Fora_do_endereco_de_uma_funeraria_so_o_admin_entra()
    {
        (Guid tenant, _) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funeraria));

        HttpResponseMessage gestor = await LoginAsync(slug: null, email, FunerariaApiFactory.SenhaPadrao);
        HttpResponseMessage admin = await LoginAsync(slug: null, FunerariaApiFactory.EmailAdmin, FunerariaApiFactory.SenhaPadrao);

        await AssertProblemAsync(gestor, HttpStatusCode.Forbidden, "Auth.FunerariaNaoIdentificada");

        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        JsonObject body = (await admin.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        var jwt = new JsonWebToken(body["access_token"]!.GetValue<string>());
        Assert.Equal("ADMIN", jwt.GetClaim("role").Value);
        Assert.False(jwt.TryGetClaim("id_tenant", out _));
    }

    [Fact]
    public async Task Cinco_senhas_erradas_bloqueiam_a_conta_mesmo_para_a_senha_certa()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));

        for (int tentativa = 1; tentativa <= 4; tentativa++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(slug, email, "Errada@123")).StatusCode);
        }

        await AssertProblemAsync(await LoginAsync(slug, email, "Errada@123"), (HttpStatusCode)423, "Auth.ContaBloqueada");
        await AssertProblemAsync(await LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao), (HttpStatusCode)423, "Auth.ContaBloqueada");
    }

    [Fact]
    public async Task Usuario_inativo_nao_entra()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (Guid idUsuario, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        await factory.DesativarUsuarioAsync(idUsuario);

        await AssertProblemAsync(await LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao), HttpStatusCode.Forbidden, "Auth.UsuarioInativo");
    }

    [Fact]
    public async Task Refresh_troca_o_cookie_e_emite_novo_access_token()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        string cookie = LerCookie(await LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao))!;

        HttpResponseMessage response = await RefreshAsync(slug, cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string novoCookie = LerCookie(response)!;
        Assert.NotEqual(cookie, novoCookie);
        JsonObject body = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("FUNCIONARIO", new JsonWebToken(body["access_token"]!.GetValue<string>()).GetClaim("role").Value);
    }

    [Fact]
    public async Task Reuso_de_refresh_token_ja_trocado_derruba_todas_as_sessoes()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        string original = LerCookie(await LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao))!;
        string outraSessao = LerCookie(await LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao))!;
        string renovado = LerCookie(await RefreshAsync(slug, original))!;

        HttpResponseMessage reuso = await RefreshAsync(slug, original);

        await AssertProblemAsync(reuso, HttpStatusCode.Unauthorized, "Auth.SessaoInvalida");
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(slug, renovado)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(slug, outraSessao)).StatusCode);
    }

    [Fact]
    public async Task Logout_revoga_o_refresh_token_e_limpa_o_cookie()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        string cookie = LerCookie(await LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao))!;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Add("Cookie", $"{Cookie}={cookie}");
        HttpResponseMessage logout = await Cliente(slug).SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(string.Empty, LerCookie(logout));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(slug, cookie)).StatusCode);
    }

    [Fact]
    public async Task Refresh_feito_no_endereco_de_outra_funeraria_e_recusado()
    {
        (Guid tenantA, string slugA) = await factory.CriarFunerariaAsync();
        (Guid tenantB, string slugB) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenantA, PerfilVinculo.Cliente), (tenantB, PerfilVinculo.Cliente));
        string cookieA = LerCookie(await LoginAsync(slugA, email, FunerariaApiFactory.SenhaPadrao))!;

        await AssertProblemAsync(await RefreshAsync(slugB, cookieA), HttpStatusCode.Unauthorized, "Auth.SessaoInvalida");
    }

    [Fact]
    public async Task Vinculo_desativado_depois_do_login_impede_a_renovacao()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (Guid idUsuario, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        string cookie = LerCookie(await LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao))!;

        await factory.DesativarVinculoAsync(idUsuario, tenant);

        await AssertProblemAsync(await RefreshAsync(slug, cookie), HttpStatusCode.Forbidden, "Auth.SemAcessoFuneraria");
    }

    [Fact]
    public async Task Minhas_funerarias_lista_os_vinculos_ativos_do_usuario()
    {
        (Guid tenantA, string slugA) = await factory.CriarFunerariaAsync();
        (Guid tenantB, _) = await factory.CriarFunerariaAsync();
        (Guid tenantC, _) = await factory.CriarFunerariaAsync();
        (Guid idUsuario, string email) = await factory.CriarUsuarioAsync(
            (tenantA, PerfilVinculo.Cliente), (tenantB, PerfilVinculo.Funcionario), (tenantC, PerfilVinculo.Cliente));
        await factory.DesativarVinculoAsync(idUsuario, tenantC);

        JsonObject login = (await (await LoginAsync(slugA, email, FunerariaApiFactory.SenhaPadrao))
            .Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        HttpClient client = Cliente(slugA);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login["access_token"]!.GetValue<string>());

        JsonArray funerarias = (await client.GetFromJsonAsync<JsonArray>("/api/v1/auth/minhas-funerarias", Json, Ct))!;

        Dictionary<Guid, string> perfis = funerarias.ToDictionary(
            f => f!["id_tenant"]!.GetValue<Guid>(),
            f => f!["perfil"]!.GetValue<string>());
        Assert.Equal(2, perfis.Count);
        Assert.Equal("CLIENTE", perfis[tenantA]);
        Assert.Equal("FUNCIONARIO", perfis[tenantB]);
    }

    [Fact]
    public async Task Minhas_funerarias_exige_autenticacao()
    {
        HttpResponseMessage response = await Cliente(slug: null).GetAsync("/api/v1/auth/minhas-funerarias", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient Cliente(string? slug)
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        if (slug is not null)
        {
            client.DefaultRequestHeaders.Add("X-Tenant", slug);
        }

        return client;
    }

    private Task<HttpResponseMessage> LoginAsync(string? slug, string email, string senha) =>
        Cliente(slug).PostAsJsonAsync("/api/v1/auth/login", new { email, senha }, Json, Ct);

    private Task<HttpResponseMessage> RefreshAsync(string slug, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"{Cookie}={cookie}");
        return Cliente(slug).SendAsync(request, Ct);
    }

    /// <summary>Valor do cookie de refresh na resposta; string vazia quando o cookie foi apagado.</summary>
    private static string? LerCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies))
        {
            return null;
        }

        string? cookie = cookies.FirstOrDefault(c => c.StartsWith(Cookie + "=", StringComparison.Ordinal));
        return cookie?[(Cookie.Length + 1)..].Split(';')[0];
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        JsonObject problem = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal(code, problem["code"]!.GetValue<string>());
    }
}
