using Funeraria.BuildingBlocks.Domain.Events;

namespace Funeraria.Modules.Planos.Domain.Planos.Events;

public sealed record ValorPlanoAlteradoDomainEvent(Guid IdPlano) : DomainEvent;
