using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.Modules.Tenants.Contracts;
using Funeraria.Testing;
using Microsoft.AspNetCore.Hosting;

namespace Funeraria.Modules.Terceiros.IntegrationTests;

/// <summary>API com o seed de demonstração ligado, como em desenvolvimento.</summary>
public sealed class DemonstracaoApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public ValueTask InitializeAsync() => IniciarAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Seed:Demo", "true");
        builder.UseSetting("Seed:SenhaDemo", SenhaPadrao);
    }
}

[CollectionDefinition(Nome)]
public sealed class DemonstracaoCollection : ICollectionFixture<DemonstracaoApiFactory>
{
    public const string Nome = "Demonstracao";
}

/// <summary>INF-16: a funerária "demo" sai pronta para usar, com planos, catálogo, equipe, cliente e um terceiro de cada tipo.</summary>
[Collection(DemonstracaoCollection.Nome)]
public sealed class DemonstracaoTests(DemonstracaoApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Funeraria_demo_tem_planos_catalogo_e_terceiros_prontos_para_uso()
    {
        const string slug = FunerariaDemonstracao.Slug;
        HttpClient publico = factory.Http(slug);

        // Planos e catálogo vêm do evento FunerariaCriada, pelo outbox.
        JsonArray planos = await AguardarAsync(() => publico.GetFromJsonAsync<JsonArray>("/api/v1/planos", Json, Ct), n => n.Count >= 3);
        JsonArray catalogo = await AguardarAsync(() => publico.GetFromJsonAsync<JsonArray>("/api/v1/catalogo", Json, Ct), n => n.Count > 0);
        Assert.NotEmpty(catalogo);

        string tokenGestor = await factory.EntrarAsync(slug, FunerariaDemonstracao.EmailGestor);
        HttpClient gestor = factory.Http(slug, tokenGestor);
        JsonObject terceiros = await AguardarAsync(
            () => gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/terceiros?status=ATIVO", Json, Ct),
            n => n["total"]!.GetValue<int>() == 3);
        Assert.Equal(
            ["CARTORIO_DESPACHANTE", "FORNECEDOR", "FROTA"],
            terceiros["itens"]!.AsArray().Select(t => t!["tipo"]!.GetValue<string>()).Order());

        foreach (string email in new[] { FunerariaDemonstracao.EmailFrota, FunerariaDemonstracao.EmailDespachante, FunerariaDemonstracao.EmailFornecedor })
        {
            Assert.False(string.IsNullOrEmpty(await factory.EntrarAsync(slug, email)));
        }

        // O cliente de demonstração já consegue contratar um plano.
        HttpClient cliente = factory.Http(slug, await factory.EntrarAsync(slug, "cliente@demo.localhost"));
        HttpResponseMessage contrato = await cliente.PostAsJsonAsync("/api/v1/contratos", new
        {
            id_plano = planos[0]!["id_plano"]!.GetValue<Guid>(),
            tipo_cobertura = "TITULAR",
            dependentes = Array.Empty<Guid>(),
            periodicidade = "MENSAL"
        }, Json, Ct);
        Assert.True(contrato.StatusCode == HttpStatusCode.Created, await contrato.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<T> AguardarAsync<T>(Func<Task<T?>> consultar, Func<T, bool> pronto)
        where T : class
    {
        for (int i = 0; i < 75; i++)
        {
            if (await consultar() is { } valor && pronto(valor))
            {
                return valor;
            }

            await Task.Delay(200, Ct);
        }

        throw new TimeoutException("A funerária de demonstração não ficou pronta.");
    }
}
