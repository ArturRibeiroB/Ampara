using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Funeral.Application.Abstractions;
using Funeraria.Modules.Clientes.Contracts.IntegrationEvents;
using Funeraria.Modules.Funeral.Contracts.IntegrationEvents;
using Funeraria.Modules.Funeral.Domain.Beneficiarios;
using Funeraria.Modules.Funeral.Domain.Pedidos;
using Funeraria.Modules.Funeral.Domain.Preferencias;
using Funeraria.Modules.Terceiros.Contracts;

namespace Funeraria.Modules.Funeral.Application.Retencao;

/// <summary>Titular anonimizado (SEG-01): as preferências de funeral dele e dos dependentes são excluídas. Idempotente.</summary>
internal sealed class ExcluirPreferenciasDoClienteAnonimizado(IPreferenciaRepository preferencias)
    : IIntegrationEventHandler<ClienteAnonimizadoIntegrationEvent>
{
    public Task Handle(ClienteAnonimizadoIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        preferencias.ExcluirDoTitularAsync(integrationEvent.IdCliente, cancellationToken);
}

/// <summary>
/// Pelo outbox: os resumos dos pedidos que os fornecedores veem perdem o nome do falecido e o local, e os módulos que guardam
/// cópias dos dados do falecido (Documentos, Translado) são avisados.
/// </summary>
internal sealed class BeneficiarioAnonimizadoDomainEventHandler(
    IPedidoRepository pedidos,
    IFuneralUnitOfWork unitOfWork,
    ITerceirosAtribuicoes atribuicoes,
    IEventBus eventBus,
    ITenantContext tenantContext)
    : IDomainEventHandler<BeneficiarioAnonimizadoDomainEvent>
{
    public async Task Handle(BeneficiarioAnonimizadoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        List<Guid> idsPedidos = [];
        foreach (Guid idFuneral in domainEvent.IdsFunerais)
        {
            foreach (Pedido pedido in await pedidos.ListarDoFuneralAsync(idFuneral, cancellationToken))
            {
                pedido.AnonimizarFalecido();
                idsPedidos.Add(pedido.Id);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await atribuicoes.AnonimizarResumosAsync(idsPedidos, cancellationToken);
        await eventBus.PublishAsync(
            new FalecidoAnonimizadoIntegrationEvent(tenantContext.RequiredTenantId, domainEvent.IdBeneficiario, domainEvent.IdsFunerais),
            cancellationToken);
    }
}
