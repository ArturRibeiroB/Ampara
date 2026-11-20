using System.Diagnostics;
using System.Reflection;
using Hangfire.Common;
using Hangfire.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Funeraria.BuildingBlocks.Infrastructure.Observabilidade;

/// <summary>
/// Traces e métricas por OTLP (INF-20). Sem <c>Observabilidade:OtlpEndpoint</c>, nada é coletado. Os dados saem sem o valor dos
/// parâmetros: o Npgsql manda só o SQL, e o ASP.NET Core e o HttpClient escondem a query string por padrão.
/// </summary>
public static class ObservabilidadeExtensions
{
    private const string OrigemNpgsql = "Npgsql";

    public static IServiceCollection AddObservabilidade(this IServiceCollection services, IConfiguration configuration)
    {
        string? endpoint = configuration["Observabilidade:OtlpEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return services;
        }

        var destino = new Uri(endpoint);
        string versao = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "desconhecida";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("funeraria-api", serviceVersion: versao))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o => o.Filter = contexto => !contexto.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource(OrigemNpgsql, RastreamentoDeJobs.Origem)
                .AddOtlpExporter(o => o.Endpoint = destino))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(OrigemNpgsql)
                .AddOtlpExporter(o => o.Endpoint = destino));

        return services;
    }
}

/// <summary>Um trace para cada execução de job, que não tem requisição para começar um (INF-20).</summary>
public sealed class RastreamentoDeJobs : JobFilterAttribute, IServerFilter
{
    public const string Origem = "Funeraria.Jobs";

    private const string ChaveAtividade = "funeraria:atividade";

    private static readonly ActivitySource Fonte = new(Origem);

    public void OnPerforming(PerformingContext context)
    {
        Activity? atividade = Fonte.StartActivity($"job {context.BackgroundJob.Job.Type.Name}.{context.BackgroundJob.Job.Method.Name}");
        atividade?.SetTag("job.id", context.BackgroundJob.Id);
        context.Items[ChaveAtividade] = atividade;
    }

    public void OnPerformed(PerformedContext context)
    {
        if (context.Items.TryGetValue(ChaveAtividade, out object? item) && item is Activity atividade)
        {
            if (context.Exception is not null && !context.ExceptionHandled)
            {
                atividade.SetStatus(ActivityStatusCode.Error, context.Exception.GetType().Name);
            }

            atividade.Dispose();
        }
    }
}
