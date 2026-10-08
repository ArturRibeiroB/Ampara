namespace Funeraria.BuildingBlocks.Domain.Events;

/// <summary>
/// Base dos eventos de domínio. São gravados no outbox do módulo na mesma transação da entidade
/// e despachados depois, fora da requisição.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
