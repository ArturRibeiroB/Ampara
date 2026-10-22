using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Funeraria.BuildingBlocks.Infrastructure.Jobs;

/// <summary>
/// Apaga as mensagens do outbox já processadas há mais de 30 dias, em todos os módulos. Sem isso as tabelas
/// <c>outbox_messages</c> só crescem. Mensagens com erro ficam, para investigação.
/// </summary>
internal sealed partial class LimpezaOutboxJob(IServiceProvider services, TimeProvider timeProvider, ILogger<LimpezaOutboxJob> logger)
    : IJobRecorrente
{
    public static readonly TimeSpan Retencao = TimeSpan.FromDays(30);

    public string Id => "limpeza-outbox";

    public string Cron => "30 3 * * *";

    public string Fila => FilasJobs.Manutencao;

    public async Task ExecutarAsync(CancellationToken cancellationToken)
    {
        DateTime limite = timeProvider.GetUtcNow().UtcDateTime - Retencao;

        foreach (ModuleDbContextRegistration registro in services.GetServices<ModuleDbContextRegistration>())
        {
            var context = (DbContext)services.GetRequiredService(registro.ContextType);
            int apagadas = await context.Set<OutboxMessage>()
                .Where(m => m.ProcessedOnUtc != null && m.ProcessedOnUtc < limite && m.Error == null)
                .ExecuteDeleteAsync(cancellationToken);

            LogLimpeza(logger, registro.ContextType.Name, apagadas);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox de {Contexto}: {Apagadas} mensagens antigas apagadas")]
    private static partial void LogLimpeza(ILogger logger, string contexto, int apagadas);
}
