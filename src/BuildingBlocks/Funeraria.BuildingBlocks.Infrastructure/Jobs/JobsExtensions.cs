using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.BuildingBlocks.Infrastructure.Jobs;

/// <summary>Hangfire com armazenamento no PostgreSQL (schema <c>hangfire</c>), filas e retry padrão (INF-11).</summary>
public static class JobsExtensions
{
    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        JobsOptions opcoes = configuration.GetSection(JobsOptions.Section).Get<JobsOptions>() ?? new JobsOptions();

        services.AddHangfire((sp, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(
                pg => pg.UseNpgsqlConnection(
                    configuration.GetConnectionString(DependencyInjection.ConnectionStringName)
                    ?? throw new InvalidOperationException("Connection string 'Database' não configurada.")),
                new PostgreSqlStorageOptions
                {
                    SchemaName = "hangfire",
                    PrepareSchemaIfNecessary = true,
                    QueuePollInterval = opcoes.IntervaloFila
                })
            // Política de retry padrão: 5 tentativas com espera crescente; depois o job fica como falho no painel.
            .UseFilter(new AutomaticRetryAttribute { Attempts = opcoes.Tentativas }));

        if (opcoes.ServidorHabilitado)
        {
            services.AddHangfireServer(server =>
            {
                server.Queues = FilasJobs.Todas;
                server.WorkerCount = opcoes.Workers;
                server.ServerName = $"funeraria-{Environment.MachineName}";
            });
        }

        services.AddScoped<ExecutorJobRecorrente>();
        services.AddScoped<IJobRecorrente, LimpezaOutboxJob>();
        return services;
    }

    /// <summary>Agenda (ou atualiza) todos os <see cref="IJobRecorrente"/> registrados pelos módulos.</summary>
    public static async Task AgendarJobsRecorrentesAsync(this IServiceProvider services)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var agendador = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        foreach (IJobRecorrente job in scope.ServiceProvider.GetServices<IJobRecorrente>())
        {
            string id = job.Id;
            agendador.AddOrUpdate<ExecutorJobRecorrente>(
                id,
                job.Fila,
                executor => executor.ExecutarAsync(id, CancellationToken.None),
                job.Cron,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
    }
}

/// <summary>
/// Ponto único que o Hangfire chama: resolve o job recorrente pelo id no escopo do job.
/// Assim os módulos não dependem do Hangfire, só de <see cref="IJobRecorrente"/>.
/// </summary>
public sealed class ExecutorJobRecorrente(IEnumerable<IJobRecorrente> jobs)
{
    [JobDisplayName("Recorrente: {0}")]
    public Task ExecutarAsync(string id, CancellationToken cancellationToken) =>
        (jobs.SingleOrDefault(j => j.Id == id)
            ?? throw new InvalidOperationException($"Job recorrente '{id}' não registrado."))
        .ExecutarAsync(cancellationToken);
}

public sealed class JobsOptions
{
    public const string Section = "Jobs";

    /// <summary>Liga o processamento de jobs nesta instância. Dá para separar uma instância só para jobs.</summary>
    public bool ServidorHabilitado { get; set; } = true;

    public int Workers { get; set; } = Math.Min(Environment.ProcessorCount * 2, 10);

    public int Tentativas { get; set; } = 5;

    public TimeSpan IntervaloFila { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Painel em /hangfire, só para o ADMIN.</summary>
    public bool PainelHabilitado { get; set; } = true;
}
