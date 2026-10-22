using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Identidade.Contracts;

namespace Funeraria.Modules.Clientes.Application.EventHandlers;

/// <summary>
/// Garantia do vínculo CLIENTE pelo outbox: o cadastro já cria o vínculo na hora, mas se essa chamada falhar,
/// este handler cria depois. A operação na Identidade é idempotente.
/// </summary>
internal sealed class VincularContaDomainEventHandler(IIdentidadeApi identidade, ITenantContext tenantContext)
    : IDomainEventHandler<ClienteCadastradoDomainEvent>
{
    public Task Handle(ClienteCadastradoDomainEvent domainEvent, CancellationToken cancellationToken) =>
        identidade.VincularClienteAsync(domainEvent.IdUsuario, tenantContext.RequiredTenantId, domainEvent.IdCliente, cancellationToken);
}
