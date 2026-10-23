using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Funeraria.Modules.Identidade.IntegrationTests;

/// <summary>Login de verdade (sub do token = conta real) para os testes que dependem de quem está logado.</summary>
internal static class SessaoDeTeste
{
    public const string Cookie = "funeraria_refresh";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static HttpClient Cliente(this IdentidadeApiFactory factory, string? slug, string? token = null)
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        if (slug is not null)
        {
            client.DefaultRequestHeaders.Add("X-Tenant", slug);
        }

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    public static Task<HttpResponseMessage> LoginAsync(this IdentidadeApiFactory factory, string? slug, string email, string senha) =>
        factory.Cliente(slug).PostAsJsonAsync("/api/v1/auth/login", new { email, senha }, Json, Ct);

    /// <summary>Entra e devolve o cliente HTTP autenticado, o access token e o refresh token do cookie.</summary>
    public static async Task<(HttpClient Http, string Token, string Refresh)> EntrarAsync(
        this IdentidadeApiFactory factory, string slug, string email, string senha = FunerariaApiFactory.SenhaPadrao)
    {
        HttpResponseMessage login = await factory.LoginAsync(slug, email, senha);
        Assert.True(login.StatusCode == HttpStatusCode.OK, await login.Content.ReadAsStringAsync(Ct));
        string token = (await login.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["access_token"]!.GetValue<string>();
        return (factory.Cliente(slug, token), token, LerCookie(login));
    }

    public static Task<HttpResponseMessage> RefreshAsync(this IdentidadeApiFactory factory, string slug, string refresh)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"{Cookie}={refresh}");
        return factory.Cliente(slug).SendAsync(request, Ct);
    }

    public static async Task<string> PerfilDoTokenAsync(HttpResponseMessage response)
    {
        string token = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["access_token"]!.GetValue<string>();
        return new JsonWebToken(token).GetClaim("role").Value;
    }

    public static async Task<JsonObject> ProblemaAsync(this HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.True(response.StatusCode == status, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
    }

    public static string LerCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").First(c => c.StartsWith(Cookie + "=", StringComparison.Ordinal))[(Cookie.Length + 1)..].Split(';')[0];
}
