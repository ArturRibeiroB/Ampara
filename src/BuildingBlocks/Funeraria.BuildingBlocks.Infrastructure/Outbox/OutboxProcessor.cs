using System.Text.Json;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.BuildingBlocks.Infrastructure.Events;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Funeraria.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Lê o outbox de um módulo e despacha os eventos de domínio para os handlers do módulo.
/// Usa <c>FOR UPDATE SKIP LOCKED</c>, então várias instâncias da API podem rodar juntas sem processar
/// a mesma mensagem ao mesmo tempo. Cada mensagem roda num escopo próprio, com o tenant da mensagem (TEN-01).
/// </summary>
public sealed partial class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor<TContext>> logger)
    : BackgroundService
    where TContext : ModuleDbContext
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Intervalo, timeProvider);

        do
        {
            try
            {
                while (await ProcessBatchAsync(stoppingToken) == _options.TamanhoLote)
                {
                    // Lote cheio: provavelmente há mais mensagens, lê de novo sem esperar.
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogFalhaLote(logger, ex, typeof(TContext).Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Processa um lote e devolve quantas mensagens foram lidas. Público para os testes de integração.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);

#pragma warning disable EF1002 // O schema vem do próprio DbContext, não de entrada do usuário.
        List<OutboxMessage> mensagens = await context.Set<OutboxMessage>()
            .FromSqlRaw(
                $"""
                 SELECT * FROM "{context.Schema}".outbox_messages
                 WHERE processed_on_utc IS NULL
                 ORDER BY occurred_on_utc
                 LIMIT {_options.TamanhoLote}
                 FOR UPDATE SKIP LOCKED
                 """)
            .ToListAsync(cancellationToken);
#pragma warning restore EF1002

        foreach (OutboxMessage mensagem in mensagens)
        {
            await ProcessMessageAsync(mensagem, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return mensagens.Count;
    }

    private async Task ProcessMessageAsync(OutboxMessage mensagem, CancellationToken cancellationToken)
    {
        try
        {
            Type tipo = Type.GetType(mensagem.Type, throwOnError: true)!;
            var evento = (IDomainEvent)JsonSerializer.Deserialize(mensagem.Content, tipo)!;

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(mensagem.TenantId);

            await scope.ServiceProvider.GetRequiredService<DomainEventDispatcher>()
                .DispatchAsync(evento, cancellationToken);

            mensagem.ProcessedOnUtc = timeProvider.GetUtcNow().UtcDateTime;
            mensagem.Error = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            mensagem.Attempts++;
            mensagem.Error = ex.ToString()[..Math.Min(ex.ToString().Length, 4000)];

            if (mensagem.Attempts >= _options.MaximoTentativas)
            {
                mensagem.ProcessedOnUtc = timeProvider.GetUtcNow().UtcDateTime;
                LogMensagemDescartada(logger, ex, mensagem.Id, mensagem.Type, mensagem.Attempts);
            }
            else
            {
                LogFalhaMensagem(logger, ex, mensagem.Id, mensagem.Type, mensagem.Attempts);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao ler o outbox de {Contexto}")]
    private static partial void LogFalhaLote(ILogger logger, Exception ex, string contexto);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao processar a mensagem {Id} ({Tipo}), tentativa {Tentativa}")]
    private static partial void LogFalhaMensagem(ILogger logger, Exception ex, Guid id, string tipo, int tentativa);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mensagem {Id} ({Tipo}) descartada após {Tentativas} tentativas")]
    private static partial void LogMensagemDescartada(ILogger logger, Exception ex, Guid id, string tipo, int tentativas);
}
