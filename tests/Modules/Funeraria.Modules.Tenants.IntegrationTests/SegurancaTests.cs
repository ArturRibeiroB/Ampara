using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Tenants.IntegrationTests;

/// <summary>SEG-02: CORS, cabeçalhos de segurança e revisão das rotas sem autenticação.</summary>
[Collection(ApiCollection.Nome)]
public sealed class SegurancaTests(TenantsApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Rotas que podem ser chamadas sem login. Toda rota fora desta lista exige autenticação por padrão.
    /// Ao criar uma rota pública nova, inclua aqui de propósito, depois de revisar.
    /// </summary>
    private static readonly string[] RotasPublicasAprovadas =
    [
        "GET /api/v1/catalogo",
        "GET /api/v1/catalogo/categorias",
        "GET /api/v1/catalogo/{id:guid}",
        "GET /api/v1/funerarias/{slug}/catalogo/{id:guid}/imagem",
        "GET /api/v1/funerarias/{slug}/logo",
        "GET /api/v1/planos",
        "GET /api/v1/planos/{id:guid}",
        "GET /api/v1/tenant/configuracoes",
        "POST /api/v1/auth/confirmar-email",
        "POST /api/v1/auth/definir-senha",
        "POST /api/v1/auth/esqueci-senha",
        "POST /api/v1/auth/login",
        "POST /api/v1/auth/logout",
        "POST /api/v1/auth/redefinir-senha",
        "POST /api/v1/auth/refresh",
        "POST /api/v1/clientes",
        "POST /api/v1/onboarding/funerarias",
        "GET /health",
        "GET /health/ready"
    ];

    [Theory]
    [InlineData("https://demo.localhost", true)]
    [InlineData("https://localhost", true)]
    [InlineData("http://demo.localhost", false)]   // fora de desenvolvimento, só HTTPS
    [InlineData("https://evil.com", false)]
    [InlineData("https://localhost.evil.com", false)]
    [InlineData("https://demolocalhost", false)]
    public async Task Cors_so_aceita_a_plataforma_e_os_subdominios(string origem, bool permitido)
    {
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        preflight.Headers.Add("Origin", origem);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type,x-tenant");

        HttpResponseMessage response = await factory.CreateClient().SendAsync(preflight, Ct);

        if (permitido)
        {
            Assert.Equal(origem, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        }
        else
        {
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    [Fact]
    public async Task Respostas_da_api_trazem_os_cabecalhos_de_seguranca()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/health", Ct);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("default-src 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task Rota_nova_sem_autorizacao_explicita_exige_login()
    {
        // /api/v1/admin/tenants tem policy; aqui o que importa é uma rota inexistente não virar pública por engano
        // e uma rota protegida responder 401 sem token.
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/v1/gestao/configuracoes", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Somente_as_rotas_aprovadas_sao_publicas()
    {
        IEnumerable<RouteEndpoint> rotas = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        string[] publicas = rotas
            .Where(r => r.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Where(r => r.RoutePattern.RawText is { } padrao && (padrao.StartsWith("/api/") || padrao.StartsWith("/health")))
            .SelectMany(r => (r.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(metodo => $"{metodo} {r.RoutePattern.RawText}"))
            .Order()
            .ToArray();

        Assert.Equal(RotasPublicasAprovadas.Order(), publicas);
    }
}
