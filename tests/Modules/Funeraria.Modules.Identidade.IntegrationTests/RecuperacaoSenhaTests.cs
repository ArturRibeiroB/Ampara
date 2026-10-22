using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Funeraria.Modules.Notificacoes.Application.Email;
using Funeraria.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Identidade.IntegrationTests;

/// <summary>"Esqueci minha senha" e redefinição (AUT-05).</summary>
[Collection(ApiCollection.Nome)]
public sealed class RecuperacaoSenhaTests(IdentidadeApiFactory factory)
{
    private const string Cookie = "funeraria_refresh";
    private const string NovaSenha = "Nova@Senha9";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Esqueci_senha_responde_204_tenha_o_email_conta_ou_nao()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));

        HttpResponseMessage existente = await EsqueciSenhaAsync(slug, email);
        HttpResponseMessage inexistente = await EsqueciSenhaAsync(slug, "ninguem@teste.localhost");

        Assert.Equal(HttpStatusCode.NoContent, existente.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, inexistente.StatusCode);

        EmailMensagem mensagem = await factory.Emails.AguardarAsync(email, Redefinicao);
        Assert.Contains($"http://{slug}.teste.localhost/redefinir-senha?token=", mensagem.CorpoTexto, StringComparison.Ordinal);
        Assert.Contains("30 minutos", mensagem.CorpoTexto, StringComparison.Ordinal);
        Assert.Empty(factory.Emails.Para("ninguem@teste.localhost"));
    }

    [Fact]
    public async Task Email_em_formato_invalido_retorna_400()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();

        HttpResponseMessage response = await EsqueciSenhaAsync(slug, "x@");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonObject problem = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("E-mail inválido.", problem["errors"]!["email"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Sem_vinculo_com_a_funeraria_do_endereco_nao_recebe_link()
    {
        (Guid outra, string slugOutra) = await factory.CriarFunerariaAsync();
        (_, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((outra, PerfilVinculo.Funcionario));

        Assert.Equal(HttpStatusCode.NoContent, (await EsqueciSenhaAsync(slug, email)).StatusCode);

        // Uma solicitação válida depois serve de marco: se a primeira tivesse gerado e-mail, ele já teria chegado.
        (_, string marco) = await factory.CriarUsuarioAsync((outra, PerfilVinculo.Funcionario));
        Assert.Equal(HttpStatusCode.NoContent, (await EsqueciSenhaAsync(slugOutra, marco)).StatusCode);
        await factory.Emails.AguardarAsync(marco, Redefinicao);
        await Task.Delay(500, Ct);
        Assert.DoesNotContain(factory.Emails.Para(email), Redefinicao);
    }

    [Fact]
    public async Task Admin_no_endereco_da_plataforma_recebe_link_do_painel()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await EsqueciSenhaAsync(null, FunerariaApiFactory.EmailAdmin)).StatusCode);

        EmailMensagem mensagem = await factory.Emails.AguardarAsync(
            FunerariaApiFactory.EmailAdmin, Redefinicao);
        Assert.Contains("http://admin.teste.localhost/redefinir-senha?token=", mensagem.CorpoTexto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redefinir_troca_a_senha_derruba_as_sessoes_e_o_link_so_vale_uma_vez()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        HttpResponseMessage login = await LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao);
        string refresh = LerCookie(login);

        string token = await SolicitarTokenAsync(slug, email);
        HttpResponseMessage redefinida = await RedefinirAsync(slug, token, NovaSenha);

        Assert.Equal(HttpStatusCode.NoContent, redefinida.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(slug, email, NovaSenha)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(slug, refresh)).StatusCode);

        await AssertLinkInvalidoAsync(await RedefinirAsync(slug, token, "Outra@Senha1"));
    }

    [Fact]
    public async Task Link_expirado_e_recusado()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (Guid idUsuario, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        string token = await SolicitarTokenAsync(slug, email);

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentidadeDbContext>().Set<TokenSenha>()
                .Where(t => t.IdUsuario == idUsuario)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiraEm, DateTime.UtcNow.AddMinutes(-1)), Ct);
        }

        await AssertLinkInvalidoAsync(await RedefinirAsync(slug, token, NovaSenha));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao)).StatusCode);
    }

    [Fact]
    public async Task Senha_fraca_nao_consome_o_link()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        string token = await SolicitarTokenAsync(slug, email);

        HttpResponseMessage fraca = await RedefinirAsync(slug, token, "123");

        Assert.Equal(HttpStatusCode.BadRequest, fraca.StatusCode);
        JsonObject problem = (await fraca.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.NotNull(problem["errors"]!["senha"]);
        Assert.Equal(HttpStatusCode.NoContent, (await RedefinirAsync(slug, token, NovaSenha)).StatusCode);
    }

    [Fact]
    public async Task Link_de_redefinicao_nao_serve_para_confirmar_email_nem_para_o_convite()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        string token = await SolicitarTokenAsync(slug, email);

        HttpResponseMessage confirmar = await Cliente(slug).PostAsJsonAsync("/api/v1/auth/confirmar-email", new { token }, Json, Ct);
        HttpResponseMessage convite = await Cliente(slug).PostAsJsonAsync("/api/v1/auth/definir-senha", new { token, senha = NovaSenha }, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, confirmar.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, convite.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RedefinirAsync(slug, token, NovaSenha)).StatusCode);
    }

    private async Task<string> SolicitarTokenAsync(string slug, string email)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await EsqueciSenhaAsync(slug, email)).StatusCode);
        EmailMensagem mensagem = await factory.Emails.AguardarAsync(email, Redefinicao);
        return EmailsCapturados.TokenDoLink(mensagem);
    }

    private static bool Redefinicao(EmailMensagem email) => email.CorpoTexto.Contains("/redefinir-senha?token=", StringComparison.Ordinal);

    private static async Task AssertLinkInvalidoAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonObject problem = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("Auth.LinkRedefinicaoInvalido", problem["code"]!.GetValue<string>());
        Assert.Equal("Link inválido ou expirado. Solicite uma nova redefinição de senha.", problem["detail"]!.GetValue<string>());
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

    private Task<HttpResponseMessage> EsqueciSenhaAsync(string? slug, string email) =>
        Cliente(slug).PostAsJsonAsync("/api/v1/auth/esqueci-senha", new { email }, Json, Ct);

    private Task<HttpResponseMessage> RedefinirAsync(string slug, string token, string senha) =>
        Cliente(slug).PostAsJsonAsync("/api/v1/auth/redefinir-senha", new { token, senha }, Json, Ct);

    private Task<HttpResponseMessage> LoginAsync(string slug, string email, string senha) =>
        Cliente(slug).PostAsJsonAsync("/api/v1/auth/login", new { email, senha }, Json, Ct);

    private Task<HttpResponseMessage> RefreshAsync(string slug, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"{Cookie}={cookie}");
        return Cliente(slug).SendAsync(request, Ct);
    }

    private static string LerCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith(Cookie + "=", StringComparison.Ordinal))[(Cookie.Length + 1)..].Split(';')[0];
}
