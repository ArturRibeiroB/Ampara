using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Contratos.Application.Abstractions;
using Funeraria.Modules.Contratos.Contracts.IntegrationEvents;
using Funeraria.Modules.Contratos.Domain.Contratos;
using Funeraria.Modules.Financeiro.Contracts.IntegrationEvents;
using Funeraria.Modules.Funeral.Contracts;

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
                contrato.ValorTotal,
                contrato.NomePlano,
                contrato.ValorPlano,
                contrato.ValorPorDependente,
                contrato.DataContratacao),
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

/// <summary>Primeira cobrança paga ou pendências quitadas: a cobertura passa a valer (COB-01, COB-03).</summary>
internal sealed class AtivarContratoEmDia(IContratoRepository contratos, IContratosUnitOfWork unitOfWork, IAuditoria auditoria)
    : IIntegrationEventHandler<ContratoEmDiaIntegrationEvent>
{
    public async Task Handle(ContratoEmDiaIntegrationEvent evento, CancellationToken cancellationToken)
    {
        if (await contratos.ObterAsync(evento.IdContrato, cancellationToken) is not Contrato contrato || contrato.Status == StatusContrato.Ativo)
        {
            return;
        }

        StatusContrato anterior = contrato.Status;
        contrato.Ativar();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (anterior == StatusContrato.Suspenso)
        {
            await auditoria.RegistrarAsync(new RegistroAuditoria(AcoesAuditadas.ContratoReativado, "Contrato", contrato.Id), cancellationToken);
        }
    }
}

/// <summary>Atraso além da tolerância: a cobertura fica suspensa até a quitação (COB-03).</summary>
internal sealed class SuspenderContratoInadimplente(IContratoRepository contratos, IContratosUnitOfWork unitOfWork, IAuditoria auditoria)
    : IIntegrationEventHandler<ContratoInadimplenteIntegrationEvent>
{
    public async Task Handle(ContratoInadimplenteIntegrationEvent evento, CancellationToken cancellationToken)
    {
        if (await contratos.ObterAsync(evento.IdContrato, cancellationToken) is Contrato { Status: StatusContrato.Ativo } contrato)
        {
            contrato.Suspender();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await auditoria.RegistrarAsync(new RegistroAuditoria(AcoesAuditadas.ContratoSuspenso, "Contrato", contrato.Id), cancellationToken);
        }
    }
}

/// <summary>Assinatura encerrada no Stripe ou 90 dias suspenso: o contrato é cancelado, menos com funeral em andamento (CON-03).</summary>
internal sealed class CancelarContratoSemAssinatura(
    IContratoRepository contratos,
    IContratosUnitOfWork unitOfWork,
    IFuneralApi funeral,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : IIntegrationEventHandler<AssinaturaEncerradaIntegrationEvent>
{
    public async Task Handle(AssinaturaEncerradaIntegrationEvent evento, CancellationToken cancellationToken)
    {
        if (await contratos.ObterAsync(evento.IdContrato, cancellationToken) is not Contrato contrato
            || contrato.Status == StatusContrato.Cancelado
            || await funeral.PossuiFuneralEmAndamentoAsync(contrato.Id, cancellationToken))
        {
            return;
        }

        contrato.Cancelar(evento.Motivo, DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.ContratoCancelado, "Contrato", contrato.Id, ValoresNovos: new { motivo = evento.Motivo }), cancellationToken);
    }
}

/// <summary>Reajuste anual: o contrato passa a mostrar os valores novos (COB-08).</summary>
internal sealed class ReajustarContrato(IContratoRepository contratos, IContratosUnitOfWork unitOfWork, IAuditoria auditoria)
    : IIntegrationEventHandler<ContratoReajustadoIntegrationEvent>
{
    public async Task Handle(ContratoReajustadoIntegrationEvent evento, CancellationToken cancellationToken)
    {
        if (await contratos.ObterAsync(evento.IdContrato, cancellationToken) is not Contrato contrato || contrato.ValorPlano == evento.ValorPlano)
        {
            return;
        }

        decimal anterior = contrato.ValorTotal;
        contrato.Reajustar(evento.ValorPlano, evento.ValorPorDependente);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.ContratoReajustado, "Contrato", contrato.Id,
            ValoresAnteriores: new { valor_total = anterior }, ValoresNovos: new { valor_total = contrato.ValorTotal }), cancellationToken);
    }
}
