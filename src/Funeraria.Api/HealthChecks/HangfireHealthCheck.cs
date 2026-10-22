using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Funeraria.Api.HealthChecks;

/// <summary>
/// Hangfire saudável (INF-13): o armazenamento responde e, se esta instância processa jobs, há servidor ativo.
/// Jobs falhos aparecem como "degradado": a API funciona, mas alguém precisa olhar o painel.
/// </summary>
internal sealed class HangfireHealthCheck(JobStorage storage, IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        JobsOptions opcoes = configuration.GetSection(JobsOptions.Section).Get<JobsOptions>() ?? new JobsOptions();

        try
        {
            var monitoramento = storage.GetMonitoringApi();
            int servidores = monitoramento.Servers().Count;
            long falhos = monitoramento.FailedCount();

            if (opcoes.ServidorHabilitado && servidores == 0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Nenhum servidor do Hangfire ativo."));
            }

            return Task.FromResult(falhos > 0
                ? HealthCheckResult.Degraded($"{falhos} job(s) falharam. Veja o painel em /hangfire.")
                : HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Armazenamento do Hangfire indisponível.", ex));
        }
    }
}
