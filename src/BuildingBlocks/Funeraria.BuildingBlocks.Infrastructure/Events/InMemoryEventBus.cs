using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Barramento em memória: entrega o evento de integração aos handlers dos outros módulos na hora.
/// Cada publicação roda num escopo próprio, com o tenant do evento: assim o módulo que reage enxerga
/// os dados da funerária certa mesmo quando quem publicou não tinha tenant (ex.: o cadastro de uma funerária).
/// </summary>
/// <remarks>
/// Os eventos de integração são publicados por handlers de eventos de domínio, que rodam a partir do outbox.
/// Se um handler de integração falhar, a mensagem do outbox volta a ser processada (entrega "pelo menos uma vez").
/// Por isso os handlers de integração precisam ser idempotentes.
/// </remarks>
internal sealed class InMemoryEventBus(IServiceScopeFactory scopeFactory) : IEventBus
{
    public async Task PublishAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        where TIntegrationEvent : IIntegrationEvent
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(integrationEvent.TenantId);

        foreach (IIntegrationEventHandler<TIntegrationEvent> handler in
                 scope.ServiceProvider.GetServices<IIntegrationEventHandler<TIntegrationEvent>>())
        {
            await handler.Handle(integrationEvent, cancellationToken);
        }
    }
}
