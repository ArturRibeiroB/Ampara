using Funeraria.BuildingBlocks.Domain.Events;

namespace Funeraria.Modules.Planos.Domain.Planos.Events;

public sealed record PlanoCriadoDomainEvent(Guid IdPlano) : DomainEvent;
