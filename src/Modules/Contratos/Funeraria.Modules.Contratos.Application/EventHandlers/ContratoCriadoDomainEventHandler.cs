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

internal sealed class PessoasDoContratoAlteradasDomainEventHandler(IContratoRepository contratos, IEventBus eventBus)
    : IDomainEventHandler<PessoasDoContratoAlteradasDomainEvent>
{
    public async Task Handle(PessoasDoContratoAlteradasDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await contratos.ObterAsync(domainEvent.IdContrato, cancellationToken) is Contrato contrato)
        {
            await eventBus.PublishAsync(
                new PessoasDoContratoAlteradasIntegrationEvent(contrato.TenantId, contrato.Id, contrato.QuantidadeDependentes, contrato.ValorTotal),
                cancellationToken);
        }
    }
}

internal sealed class ContratoCanceladoDomainEventHandler(IContratoRepository contratos, IEventBus eventBus)
    : IDomainEventHandler<ContratoCanceladoDomainEvent>
{
    public async Task Handle(ContratoCanceladoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await contratos.ObterAsync(domainEvent.IdContrato, cancellationToken) is Contrato contrato)
        {
            await eventBus.PublishAsync(
                new ContratoCanceladoIntegrationEvent(
                    contrato.TenantId, contrato.Id, contrato.IdCliente, contrato.DataFim!.Value, contrato.MotivoCancelamento!),
                cancellationToken);
        }
    }
}
