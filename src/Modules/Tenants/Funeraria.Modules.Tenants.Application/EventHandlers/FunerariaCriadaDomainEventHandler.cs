using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Tenants.Contracts.IntegrationEvents;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Modules.Tenants.Domain.Funerarias.Events;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Application.EventHandlers;

/// <summary>Avisa os outros módulos: a Identidade convida o gestor e o Planos copia os planos modelo (TEN-02).</summary>
internal sealed class FunerariaCriadaDomainEventHandler(IFunerariaRepository funerarias, IEventBus eventBus)
    : IDomainEventHandler<FunerariaCriadaDomainEvent>
{
    public async Task Handle(FunerariaCriadaDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        FunerariaEntity? funeraria = await funerarias.ObterPorIdAsync(domainEvent.IdTenant, cancellationToken);
        if (funeraria is null)
        {
            return;
        }

        await eventBus.PublishAsync(
            new FunerariaCriadaIntegrationEvent(
                funeraria.Id,
                funeraria.Slug,
                funeraria.NomeFantasia,
                domainEvent.GestorNome,
                domainEvent.GestorEmail),
            cancellationToken);
    }
}
