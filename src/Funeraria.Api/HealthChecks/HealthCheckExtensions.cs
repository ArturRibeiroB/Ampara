using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Funeraria.Api.HealthChecks;

/// <summary>
/// Health checks (INF-13): <c>/health</c> diz se o processo está de pé; <c>/health/ready</c> confere o banco.
/// As integrações externas (Stripe, API de distância) entram aqui quando forem configuradas.
/// </summary>
internal static class HealthCheckExtensions
{
    private const string Ready = "ready";

    public static IServiceCollection AddVerificacoesDeSaude(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: [Ready])
            .AddCheck<HangfireHealthCheck>("hangfire", tags: [Ready]);
        return services;
    }

    public static WebApplication MapVerificacoesDeSaude(this WebApplication app)
    {
        app.MapHealthChecks("/health", new() { Predicate = _ => false })
            .WithMetadata(new HttpMethodMetadata(["GET"]))
            .AllowAnonymous();
        app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains(Ready) })
            .WithMetadata(new HttpMethodMetadata(["GET"]))
            .AllowAnonymous();
        return app;
    }

    private sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using NpgsqlCommand command = dataSource.CreateCommand("SELECT 1");
                await command.ExecuteScalarAsync(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                return HealthCheckResult.Unhealthy("PostgreSQL indisponível.", ex);
            }
        }
    }
}
