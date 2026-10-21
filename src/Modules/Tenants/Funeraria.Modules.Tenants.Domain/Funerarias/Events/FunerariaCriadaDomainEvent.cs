using Funeraria.BuildingBlocks.Domain.Events;

namespace Funeraria.Modules.Tenants.Domain.Funerarias.Events;

/// <summary>Leva os dados do gestor a convidar: eles não ficam gravados na funerária.</summary>
public sealed record FunerariaCriadaDomainEvent(Guid IdTenant, string GestorNome, string GestorEmail) : DomainEvent;
