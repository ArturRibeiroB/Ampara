using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Contratos.Contracts.IntegrationEvents;
using Funeraria.Modules.Contratos.Domain.Contratos;

namespace Funeraria.Modules.Contratos.Application.EventHandlers;

/// <summary>Publica <see cref="ContratoCriadoIntegrationEvent"/> para o Financeiro criar a assinatura (COB-01).</summary>
internal sealed class ContratoCriadoDomainEventHandler(IContratoRepository contratos, IEventBus eventBus)
    : IDomainEventHandler<ContratoCriadoDomainEvent>
{
    public async Task Handle(ContratoCriadoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        Contrato? contrato = await contratos.ObterAsync(domainEvent.IdContrato, cancellationToken);
        if (contrato is null)
        {
            return;
        }

        await eventBus.PublishAsync(
            new ContratoCriadoIntegrationEvent(
                contrato.TenantId,
                contrato.Id,
                contrato.IdCliente,
                contrato.IdPlano,
                contrato.Periodicidade == Periodicidade.Anual ? "ANUAL" : "MENSAL",
                contrato.DiaVencimento,
                contrato.PessoasCobertas.Count(p => p.Tipo == TipoPessoaCoberta.Dependente && p.DataRemocao is null),
                contrato.ValorTotal),
            cancellationToken);
    }
}
