using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Planos.Contracts.IntegrationEvents;
using Funeraria.Modules.Planos.Domain.Planos;
using Funeraria.Modules.Planos.Domain.Planos.Events;

namespace Funeraria.Modules.Planos.Application.EventHandlers;

/// <summary>Publica <see cref="ValorPlanoAlteradoIntegrationEvent"/> com os valores atuais do plano.</summary>
internal sealed class ValorPlanoAlteradoDomainEventHandler(IPlanoRepository planos, IEventBus eventBus)
    : IDomainEventHandler<ValorPlanoAlteradoDomainEvent>
{
    public async Task Handle(ValorPlanoAlteradoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        Plano? plano = await planos.ObterPorIdAsync(domainEvent.IdPlano, cancellationToken);
        if (plano is null)
        {
            return;
        }

        await eventBus.PublishAsync(
            new ValorPlanoAlteradoIntegrationEvent(
                plano.TenantId,
                plano.Id,
                plano.Precos.ValorMensal,
                plano.Precos.ValorAnual,
                plano.Precos.ValorDependenteMensal,
                plano.Precos.ValorDependenteAnual),
            cancellationToken);
    }
}
