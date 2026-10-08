namespace Funeraria.BuildingBlocks.Application.Events;

public interface IIntegrationEvent
{
    Guid Id { get; }

    DateTime OccurredOnUtc { get; }

    /// <summary>Funerária dona do evento. Nulo em eventos da plataforma.</summary>
    Guid? TenantId { get; }
}

/// <summary>
/// Evento publicado por um módulo para os outros. Fica no projeto Contracts do módulo que publica:
/// é a API pública do módulo, junto com os DTOs (INF-01).
/// </summary>
public abstract record IntegrationEvent(Guid? TenantId) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
