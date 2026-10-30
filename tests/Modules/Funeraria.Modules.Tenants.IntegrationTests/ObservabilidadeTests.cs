using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Funeraria.Modules.Tenants.IntegrationTests;

/// <summary>INF-20: com o endpoint configurado, requisições e banco viram spans, com a funerária como atributo.</summary>
[Collection(ApiCollection.Nome)]
public sealed class ObservabilidadeTests(TenantsApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Com_endpoint_a_requisicao_gera_spans_sem_query_string_e_sem_o_health()
    {
        var coletados = new SpansColetados();
        await using var observado = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Observabilidade:OtlpEndpoint", "http://127.0.0.1:4317");
            b.UseSetting("Jobs:ServidorHabilitado", "false");
            b.ConfigureTestServices(s => s.ConfigureOpenTelemetryTracerProvider(t => t.AddInMemoryExporter(coletados)));
        });
        using HttpClient admin = observado.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(Perfis.Admin, null));

        using HttpResponseMessage lista = await admin.GetAsync("/api/v1/admin/tenants?segredo=nao-pode-vazar", Ct);
        Assert.True(lista.IsSuccessStatusCode);
        using HttpResponseMessage saude = await admin.GetAsync("/health", Ct);

        observado.Services.GetRequiredService<TracerProvider>().ForceFlush();
        Activity[] spans = coletados.Copia();

        Activity requisicao = Assert.Single(spans, a => a.Kind == ActivityKind.Server);
        Assert.Contains("/api/v1/admin/tenants", requisicao.GetTagItem("url.path")?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("nao-pode-vazar", requisicao.GetTagItem("url.query")?.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(spans, a => a.Source.Name == "Npgsql");
    }

    [Fact]
    public void A_funeraria_vira_atributo_do_trace_em_andamento()
    {
        using var ouvinte = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(ouvinte);
        using var fonte = new ActivitySource("teste-observabilidade");
        using Activity atividade = fonte.StartActivity("requisicao")!;
        var tenant = Guid.NewGuid();

        new TenantContext().Set(tenant);

        Assert.Equal(tenant.ToString(), atividade.GetTagItem("id_tenant")?.ToString());
    }

    /// <summary>O outbox e os jobs do host continuam gerando spans enquanto o teste lê.</summary>
    private sealed class SpansColetados : Collection<Activity>
    {
        private readonly Lock _trava = new();

        public Activity[] Copia()
        {
            lock (_trava)
            {
                return [.. Items];
            }
        }

        protected override void InsertItem(int index, Activity item)
        {
            lock (_trava)
            {
                base.InsertItem(index, item);
            }
        }
    }
}
