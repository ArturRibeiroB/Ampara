namespace Funeraria.BuildingBlocks.Infrastructure.Jobs;

/// <summary>
/// Job recorrente de um módulo (INF-11). O módulo registra a implementação no DI e o host agenda todas na
/// inicialização. Jobs que trabalham por funerária percorrem os tenants e definem o tenant antes de cada um (TEN-01).
/// </summary>
public interface IJobRecorrente
{
    /// <summary>Identificador estável: muda o nome, muda o agendamento no Hangfire.</summary>
    string Id { get; }

    /// <summary>Expressão CRON em UTC (ex.: "0 3 * * *" = todo dia às 03:00 UTC).</summary>
    string Cron { get; }

    /// <summary>Fila do Hangfire: <see cref="FilasJobs"/>.</summary>
    string Fila => FilasJobs.Padrao;

    Task ExecutarAsync(CancellationToken cancellationToken);
}

/// <summary>Filas do Hangfire (INF-11). E-mails e integrações não disputam com o resto.</summary>
public static class FilasJobs
{
    public const string Padrao = "default";
    public const string Emails = "emails";
    public const string Integracoes = "integracoes";
    public const string Manutencao = "manutencao";

    public static readonly string[] Todas = [Padrao, Emails, Integracoes, Manutencao];
}
