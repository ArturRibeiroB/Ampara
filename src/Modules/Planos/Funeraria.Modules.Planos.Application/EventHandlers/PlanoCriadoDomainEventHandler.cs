using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Planos.Contracts.IntegrationEvents;
using Funeraria.Modules.Planos.Domain.Planos;
using Funeraria.Modules.Planos.Domain.Planos.Events;

namespace Funeraria.Modules.Planos.Application.EventHandlers;

/// <summary>Publica <see cref="PlanoCriadoIntegrationEvent"/> para o Financeiro sincronizar com o Stripe.</summary>
internal sealed class PlanoCriadoDomainEventHandler(IPlanoRepository planos, IEventBus eventBus)
    : IDomainEventHandler<PlanoCriadoDomainEvent>
{
    public async Task Handle(PlanoCriadoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        Plano? plano = await planos.ObterPorIdAsync(domainEvent.IdPlano, cancellationToken);
        if (plano is null)
        {
            return;
        }

        await eventBus.PublishAsync(
            new PlanoCriadoIntegrationEvent(
                plano.TenantId,
                plano.Id,
                plano.Nome,
                plano.Precos.ValorMensal,
                plano.Precos.ValorAnual,
                plano.Precos.ValorDependenteMensal,
                plano.Precos.ValorDependenteAnual),
            cancellationToken);
    }
}
