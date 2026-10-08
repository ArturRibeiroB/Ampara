using Funeraria.BuildingBlocks.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Barramento em memória: entrega o evento de integração aos handlers dos outros módulos na hora,
/// no mesmo escopo de quem publicou.
/// </summary>
/// <remarks>
/// Os eventos de integração são publicados por handlers de eventos de domínio, que rodam a partir do outbox.
/// Se um handler de integração falhar, a mensagem do outbox volta a ser processada (entrega "pelo menos uma vez").
/// Por isso os handlers de integração precisam ser idempotentes.
/// </remarks>
internal sealed class InMemoryEventBus(IServiceProvider serviceProvider) : IEventBus
{
    public async Task PublishAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        where TIntegrationEvent : IIntegrationEvent
    {
        foreach (IIntegrationEventHandler<TIntegrationEvent> handler in
                 serviceProvider.GetServices<IIntegrationEventHandler<TIntegrationEvent>>())
        {
            await handler.Handle(integrationEvent, cancellationToken);
        }
    }
}
