namespace Funeraria.BuildingBlocks.Application.Events;

/// <summary>Barramento de eventos entre módulos. No monolito, em memória.</summary>
public interface IEventBus
{
    Task PublishAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        where TIntegrationEvent : IIntegrationEvent;
}
