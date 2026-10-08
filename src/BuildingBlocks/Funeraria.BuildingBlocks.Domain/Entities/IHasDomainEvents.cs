using Funeraria.BuildingBlocks.Domain.Events;

namespace Funeraria.BuildingBlocks.Domain.Entities;

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
