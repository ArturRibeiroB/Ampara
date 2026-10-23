using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Testing;
using static Funeraria.Modules.Identidade.IntegrationTests.SessaoDeTeste;

namespace Funeraria.Modules.Identidade.IntegrationTests;

/// <summary>AUT-06: o usuário logado troca a própria senha.</summary>
[Collection(ApiCollection.Nome)]
public sealed class AlterarSenhaTests(IdentidadeApiFactory factory)
{
    private const string NovaSenha = "Nova@Senha9";

    [Fact]
    public async Task Troca_a_senha_e_encerra_as_sessoes()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        (HttpClient http, _, string refresh) = await factory.EntrarAsync(slug, email);

        HttpResponseMessage response = await http.PutAsJsonAsync("/api/v1/auth/senha",
            new { senha_atual = FunerariaApiFactory.SenhaPadrao, nova_senha = NovaSenha }, Json, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.LoginAsync(slug, email, NovaSenha)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.RefreshAsync(slug, refresh)).StatusCode);
    }

    [Fact]
    public async Task Senha_atual_errada_e_recusada()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        (HttpClient http, _, _) = await factory.EntrarAsync(slug, email);

        JsonObject problem = await (await http.PutAsJsonAsync("/api/v1/auth/senha",
            new { senha_atual = "Errada@123", nova_senha = NovaSenha }, Json, Ct)).ProblemaAsync(HttpStatusCode.BadRequest);

        Assert.Equal("Senha atual incorreta.", problem["errors"]!["senha_atual"]![0]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await factory.LoginAsync(slug, email, FunerariaApiFactory.SenhaPadrao)).StatusCode);
    }

    [Fact]
    public async Task Nova_senha_fraca_ou_igual_a_atual_e_recusada()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funcionario));
        (HttpClient http, _, _) = await factory.EntrarAsync(slug, email);

        JsonObject fraca = await (await http.PutAsJsonAsync("/api/v1/auth/senha",
            new { senha_atual = FunerariaApiFactory.SenhaPadrao, nova_senha = "123" }, Json, Ct)).ProblemaAsync(HttpStatusCode.BadRequest);
        JsonObject igual = await (await http.PutAsJsonAsync("/api/v1/auth/senha",
            new { senha_atual = FunerariaApiFactory.SenhaPadrao, nova_senha = FunerariaApiFactory.SenhaPadrao }, Json, Ct))
            .ProblemaAsync(HttpStatusCode.BadRequest);

        Assert.NotNull(fraca["errors"]!["nova_senha"]);
        Assert.Equal("A nova senha deve ser diferente da atual.", igual["errors"]!["nova_senha"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Sem_login_retorna_401()
    {
        HttpResponseMessage response = await factory.Cliente(slug: null).PutAsJsonAsync("/api/v1/auth/senha",
            new { senha_atual = "x", nova_senha = NovaSenha }, Json, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
