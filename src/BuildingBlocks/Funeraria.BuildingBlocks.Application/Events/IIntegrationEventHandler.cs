namespace Funeraria.BuildingBlocks.Application.Events;

/// <summary>
/// Reage a um evento de integração publicado por outro módulo. Pode rodar mais de uma vez: precisa ser idempotente.
/// </summary>
public interface IIntegrationEventHandler<in TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    Task Handle(TIntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
