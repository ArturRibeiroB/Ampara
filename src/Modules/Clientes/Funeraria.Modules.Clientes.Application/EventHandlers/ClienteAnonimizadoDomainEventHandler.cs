using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Clientes.Contracts.IntegrationEvents;
using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Identidade.Contracts;

namespace Funeraria.Modules.Clientes.Application.EventHandlers;

/// <summary>
/// Pelo outbox, depois da anonimização (SEG-01): a Identidade encerra o acesso da conta à funerária e os outros módulos
/// recebem <see cref="ClienteAnonimizadoIntegrationEvent"/>. As duas pontas são idempotentes.
/// </summary>
internal sealed class ClienteAnonimizadoDomainEventHandler(IIdentidadeApi identidade, IEventBus eventBus, ITenantContext tenantContext)
    : IDomainEventHandler<ClienteAnonimizadoDomainEvent>
{
    public async Task Handle(ClienteAnonimizadoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        Guid idTenant = tenantContext.RequiredTenantId;
        await identidade.EncerrarAcessoDoClienteAnonimizadoAsync(domainEvent.IdUsuario, idTenant, cancellationToken);
        await eventBus.PublishAsync(new ClienteAnonimizadoIntegrationEvent(idTenant, domainEvent.IdCliente), cancellationToken);
    }
}
