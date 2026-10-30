using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Tenants.IntegrationTests;

/// <summary>INF-21: o openapi.json traz a versão do contrato e marca como deprecated o que está obsoleto.</summary>
[Collection(ApiCollection.Nome)]
public sealed class ContratoOpenApiTests(TenantsApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Documento_tem_a_versao_do_contrato_e_marca_o_que_esta_obsoleto()
    {
        using WebApplicationFactory<Program> comDocumentacao = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Documentacao:Habilitada", "true");
            b.UseSetting("Seed:Executar", "false");

            // Segundo host no mesmo processo: sem servidor do Hangfire (a configuração dele é global, ver DemonstracaoTests).
            b.UseSetting("Jobs:ServidorHabilitado", "false");
            b.ConfigureTestServices(s => s.AddTransient<IEndpoint, EndpointObsoleto>());
        });

        JsonObject documento = JsonNode.Parse(await comDocumentacao.CreateClient().GetStringAsync("/openapi/v1.json", Ct))!.AsObject();

        // Fora de um release, X.Y.Z-dev; na tag vX.Y.Z, X.Y.Z (o CI passa a versão no build).
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$", documento["info"]!["version"]!.GetValue<string>());

        JsonNode operacao = documento["paths"]!["/api/v1/teste/obsoleto"]!["get"]!;
        Assert.True(operacao["deprecated"]!.GetValue<bool>());

        JsonNode propriedades = documento["components"]!["schemas"]![nameof(RespostaComCampoObsoleto)]!["properties"]!;
        Assert.True(propriedades["nome_antigo"]!["deprecated"]!.GetValue<bool>());
        Assert.Null(propriedades["nome"]!["deprecated"]);
    }

    public sealed record RespostaComCampoObsoleto(string Nome, [property: Obsolete("Use nome.")] string NomeAntigo);

    private sealed class EndpointObsoleto : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("teste/obsoleto", () => Results.Ok())
                .Produces<RespostaComCampoObsoleto>()
                .MarcarObsoleto("GET /api/v1/teste/novo");
    }
}
