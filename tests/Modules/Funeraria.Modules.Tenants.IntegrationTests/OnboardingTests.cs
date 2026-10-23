using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.Modules.Notificacoes.Application.Email;
using Funeraria.Testing;

namespace Funeraria.Modules.Tenants.IntegrationTests;

/// <summary>TEN-02 de ponta a ponta: cadastro → convite por e-mail → definir senha → login → planos modelo.</summary>
[Collection(ApiCollection.Nome)]
public sealed class OnboardingTests(TenantsApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Auto_cadastro_convida_o_gestor_que_define_a_senha_e_entra_na_funeraria()
    {
        (string slug, string emailGestor) = NovosDados();

        HttpResponseMessage cadastro = await Cliente().PostAsJsonAsync("/api/v1/onboarding/funerarias", Corpo(slug, emailGestor), Json, Ct);

        Assert.Equal(HttpStatusCode.Created, cadastro.StatusCode);
        JsonObject criada = (await cadastro.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("PENDENTE", criada["status"]!.GetValue<string>());
        Assert.Equal(slug, criada["slug"]!.GetValue<string>());

        EmailMensagem convite = await factory.Emails.AguardarAsync(emailGestor);
        Assert.Contains("Convite", convite.Assunto, StringComparison.Ordinal);
        Assert.Contains($"http://{slug}.teste.localhost/definir-senha?token=", convite.CorpoTexto, StringComparison.Ordinal);

        HttpResponseMessage definir = await DefinirSenhaAsync(EmailsCapturados.TokenDoLink(convite), "NovaSenha@123");
        Assert.Equal(HttpStatusCode.NoContent, definir.StatusCode);

        HttpResponseMessage login = await Cliente(slug).PostAsJsonAsync(
            "/api/v1/auth/login", new { email = emailGestor, senha = "NovaSenha@123" }, Json, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        JsonObject sessao = (await login.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("FUNERARIA", sessao["usuario"]!["role"]!.GetValue<string>());

        // Planos modelo copiados, mas inativos: não aparecem para o cliente até a funerária revisar.
        HttpClient gestor = Cliente(slug, sessao["access_token"]!.GetValue<string>());
        JsonArray planos = await AguardarPlanosModeloAsync(gestor);
        Assert.Equal(["Essencial", "Familiar", "Premium"], planos.Select(p => p!["nome"]!.GetValue<string>()).Order());
        Assert.All(planos, p => Assert.False(p!["ativo"]!.GetValue<bool>()));
        Assert.Empty((await Cliente(slug).GetFromJsonAsync<JsonArray>("/api/v1/planos", Json, Ct))!);

        JsonObject configuracoes = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/configuracoes", Json, Ct))!;
        Assert.Equal("Funerária Teste", configuracoes["identidade_visual"]!["nome_exibicao"]!.GetValue<string>());
        Assert.True(configuracoes["cobranca_configurada"]!.GetValue<bool>()); // Padrões do DEC-12
    }

    [Fact]
    public async Task Link_do_convite_vale_uma_vez_e_senha_fraca_nao_o_consome()
    {
        (string slug, string emailGestor) = NovosDados();
        await CadastrarAsync(slug, emailGestor);
        string token = EmailsCapturados.TokenDoLink(await factory.Emails.AguardarAsync(emailGestor));

        HttpResponseMessage fraca = await DefinirSenhaAsync(token, "fraca");
        Assert.Equal(HttpStatusCode.BadRequest, fraca.StatusCode);
        JsonObject erros = (await fraca.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["errors"]!.AsObject();
        Assert.Contains("A senha deve ter pelo menos 8 caracteres.", erros["senha"]!.AsArray().Select(e => e!.GetValue<string>()));

        Assert.Equal(HttpStatusCode.NoContent, (await DefinirSenhaAsync(token, "NovaSenha@123")).StatusCode);

        HttpResponseMessage reuso = await DefinirSenhaAsync(token, "OutraSenha@123");
        Assert.Equal(HttpStatusCode.BadRequest, reuso.StatusCode);
        Assert.Equal("Auth.LinkSenhaInvalido", (await reuso.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["code"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await DefinirSenhaAsync("token-inventado", "NovaSenha@123")).StatusCode);
    }

    [Fact]
    public async Task Gestor_que_ja_tem_conta_recebe_aviso_de_acesso_e_usa_a_mesma_senha()
    {
        (string slugA, string emailGestor) = NovosDados();
        await CadastrarAsync(slugA, emailGestor);
        string token = EmailsCapturados.TokenDoLink(await factory.Emails.AguardarAsync(emailGestor));
        await DefinirSenhaAsync(token, "NovaSenha@123");

        (string slugB, _) = NovosDados();
        await CadastrarAsync(slugB, emailGestor);

        EmailMensagem aviso = await factory.Emails.AguardarAsync(emailGestor, e => e.Assunto.StartsWith("Acesso liberado", StringComparison.Ordinal));
        Assert.DoesNotContain("token=", aviso.CorpoTexto, StringComparison.Ordinal);

        HttpResponseMessage login = await Cliente(slugB).PostAsJsonAsync(
            "/api/v1/auth/login", new { email = emailGestor, senha = "NovaSenha@123" }, Json, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Cadastro_invalido_retorna_os_erros_por_campo()
    {
        var corpo = new
        {
            nome_fantasia = "",
            razao_social = "Razão",
            cnpj = "11.222.333/0001-80",
            slug = "api",
            email_contato = "nao-e-email",
            gestor = new { nome = "Maria", email = "" }
        };

        HttpResponseMessage response = await Cliente().PostAsJsonAsync("/api/v1/onboarding/funerarias", corpo, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonObject erros = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["errors"]!.AsObject();
        Assert.Equal("CNPJ não informado ou em formato inválido.", erros["cnpj"]![0]!.GetValue<string>());
        Assert.Equal("Este endereço é reservado pela plataforma. Escolha outro.", erros["slug"]![0]!.GetValue<string>());
        Assert.NotNull(erros["nome_fantasia"]);
        Assert.NotNull(erros["email_contato"]);
        Assert.NotNull(erros["gestor_email"]);
    }

    [Fact]
    public async Task Cnpj_e_endereco_sao_unicos_na_plataforma()
    {
        (string slug, string email) = NovosDados();
        string cnpj = DocumentosDeTeste.NovoCnpj();
        await CadastrarAsync(slug, email, cnpj);

        HttpResponseMessage mesmoCnpj = await Cliente().PostAsJsonAsync(
            "/api/v1/onboarding/funerarias", Corpo(NovosDados().Slug, email, cnpj), Json, Ct);
        HttpResponseMessage mesmoSlug = await Cliente().PostAsJsonAsync(
            "/api/v1/onboarding/funerarias", Corpo(slug, email), Json, Ct);

        await AssertProblemAsync(mesmoCnpj, HttpStatusCode.Conflict, "Funeraria.CnpjDuplicado");
        await AssertProblemAsync(mesmoSlug, HttpStatusCode.Conflict, "Funeraria.SlugEmUso");
    }

    [Fact]
    public async Task Cnpj_alfanumerico_e_aceito()
    {
        (string slug, string email) = NovosDados();
        string alfanumerico = $"{Random.Shared.Next(10, 99)}AB{Random.Shared.Next(1000, 9999)}0001";
        alfanumerico += DigitosCnpj(alfanumerico);

        HttpResponseMessage response = await Cliente().PostAsJsonAsync(
            "/api/v1/onboarding/funerarias", Corpo(slug, email, alfanumerico), Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<JsonArray> AguardarPlanosModeloAsync(HttpClient gestor)
    {
        DateTime limite = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            JsonArray planos = (await gestor.GetFromJsonAsync<JsonArray>("/api/v1/gestao/planos", Json, Ct))!;
            if (planos.Count > 0 || DateTime.UtcNow > limite)
            {
                return planos;
            }

            await Task.Delay(100, Ct);
        }
    }

    private async Task CadastrarAsync(string slug, string emailGestor, string? cnpj = null)
    {
        HttpResponseMessage response = await Cliente().PostAsJsonAsync(
            "/api/v1/onboarding/funerarias", Corpo(slug, emailGestor, cnpj), Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private Task<HttpResponseMessage> DefinirSenhaAsync(string token, string senha) =>
        Cliente().PostAsJsonAsync("/api/v1/auth/definir-senha", new { token, senha }, Json, Ct);

    private HttpClient Cliente(string? slug = null, string? accessToken = null)
    {
        HttpClient client = factory.CreateClient();
        if (slug is not null)
        {
            client.DefaultRequestHeaders.Add("X-Tenant", slug);
        }

        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }

    private static (string Slug, string EmailGestor) NovosDados()
    {
        string id = Guid.NewGuid().ToString("N")[..12];
        return ($"fun-{id}", $"gestor-{id}@teste.localhost");
    }

    private static object Corpo(string slug, string emailGestor, string? cnpj = null) => new
    {
        nome_fantasia = "Funerária Teste",
        razao_social = "Funerária Teste Ltda",
        cnpj = cnpj ?? DocumentosDeTeste.NovoCnpj(),
        slug,
        email_contato = $"contato@{slug}.localhost",
        telefone = "(11) 3333-4444",
        gestor = new { nome = "Maria Gestora", email = emailGestor }
    };

    private static string DigitosCnpj(string doze)
    {
        int Dv(string v, int[] pesos)
        {
            int resto = v.Select((c, i) => (c - '0') * pesos[i]).Sum() % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        int d1 = Dv(doze, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        int d2 = Dv(doze + d1, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return $"{d1}{d2}";
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["code"]!.GetValue<string>());
    }
}
