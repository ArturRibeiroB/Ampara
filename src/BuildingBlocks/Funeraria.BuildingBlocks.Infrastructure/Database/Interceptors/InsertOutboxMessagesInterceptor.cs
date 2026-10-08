using System.Text.Json;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Tenancy;
using Funeraria.BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Funeraria.BuildingBlocks.Infrastructure.Database.Interceptors;

/// <summary>
/// Converte os eventos de domínio dos agregados em mensagens do outbox, gravadas na mesma transação.
/// Assim um evento nunca é publicado para uma alteração que não foi salva, nem perdido se a API cair.
/// </summary>
public sealed class InsertOutboxMessagesInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        InsertOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        InsertOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void InsertOutboxMessages(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var mensagens = new List<OutboxMessage>();

        foreach (IHasDomainEvents entidade in context.ChangeTracker.Entries<IHasDomainEvents>().Select(e => e.Entity))
        {
            Guid? tenantId = entidade is ITenantEntity t ? t.TenantId : tenantContext.TenantId;

            foreach (IDomainEvent evento in entidade.DomainEvents)
            {
                Type tipo = evento.GetType();
                mensagens.Add(new OutboxMessage
                {
                    Id = evento.Id,
                    Type = OutboxMessage.TypeName(tipo),
                    Content = JsonSerializer.Serialize(evento, tipo),
                    TenantId = tenantId,
                    OccurredOnUtc = evento.OccurredOnUtc
                });
            }

            entidade.ClearDomainEvents();
        }

        if (mensagens.Count > 0)
        {
            context.Set<OutboxMessage>().AddRange(mensagens);
        }
    }
}
