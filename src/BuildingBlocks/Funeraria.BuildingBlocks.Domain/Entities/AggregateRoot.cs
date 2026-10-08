using Funeraria.BuildingBlocks.Domain.Events;

namespace Funeraria.BuildingBlocks.Domain.Entities;

/// <summary>
/// Raiz de agregado: único ponto de entrada para alterar o agregado e quem levanta os eventos de domínio.
/// Recebe a coluna xmin do PostgreSQL como token de concorrência otimista (INF-03).
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
