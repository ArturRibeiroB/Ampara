using Funeraria.BuildingBlocks.Domain.Events;

namespace Funeraria.BuildingBlocks.Application.Events;

/// <summary>
/// Reage a um evento de domínio do próprio módulo. Roda no processamento do outbox,
/// fora da requisição e com o tenant da mensagem. Pode rodar mais de uma vez: precisa ser idempotente.
/// </summary>
public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    Task Handle(TDomainEvent domainEvent, CancellationToken cancellationToken);
}
