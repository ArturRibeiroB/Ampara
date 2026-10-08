namespace Funeraria.BuildingBlocks.Domain.Events;

public interface IDomainEvent
{
    Guid Id { get; }

    DateTime OccurredOnUtc { get; }
}
