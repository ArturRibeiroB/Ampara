using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Identidade.Contracts.IntegrationEvents;
using Funeraria.Modules.Terceiros.Application.Abstractions;
using Funeraria.Modules.Terceiros.Contracts.IntegrationEvents;
using Funeraria.Modules.Terceiros.Domain.Terceiros;

namespace Funeraria.Modules.Terceiros.Application.EventHandlers;

/// <summary>Publica o cadastro para a Identidade criar a conta, o vínculo e o convite (TER-01).</summary>
internal sealed class TerceiroCadastradoDomainEventHandler(ITerceiroRepository terceiros, IEventBus eventBus)
    : IDomainEventHandler<TerceiroCadastradoDomainEvent>
{
    public async Task Handle(TerceiroCadastradoDomainEvent evento, CancellationToken cancellationToken)
    {
        if (await terceiros.ObterAsync(evento.IdTerceiro, cancellationToken) is not Terceiro t)
        {
            return;
        }

        await eventBus.PublishAsync(
            new TerceiroCadastradoIntegrationEvent(t.TenantId, t.Id, TipoParaClaim(t.Tipo), t.NomeRazaoSocial, t.Email), cancellationToken);
    }

    /// <summary>Mesmo valor da claim <c>tipo_terceiro</c> do token (DEC-06).</summary>
    internal static string TipoParaClaim(TipoTerceiro tipo) => tipo switch
    {
        TipoTerceiro.Frota => TiposTerceiro.Frota,
        TipoTerceiro.CartorioDespachante => TiposTerceiro.CartorioDespachante,
        _ => TiposTerceiro.Fornecedor
    };
}

/// <summary>Publica a ativação ou desativação para a Identidade ligar ou desligar o acesso (TER-01).</summary>
internal sealed class TerceiroStatusAlteradoDomainEventHandler(ITerceiroRepository terceiros, IEventBus eventBus)
    : IDomainEventHandler<TerceiroStatusAlteradoDomainEvent>
{
    public async Task Handle(TerceiroStatusAlteradoDomainEvent evento, CancellationToken cancellationToken)
    {
        if (await terceiros.ObterAsync(evento.IdTerceiro, cancellationToken) is Terceiro t)
        {
            await eventBus.PublishAsync(new TerceiroStatusAlteradoIntegrationEvent(t.TenantId, t.Id, evento.Ativo), cancellationToken);
        }
    }
}

/// <summary>A Identidade ligou o terceiro a uma conta: guarda a conta e, se ela já tem senha, ativa o parceiro.</summary>
internal sealed class TerceiroVinculadoIntegrationEventHandler(ITerceiroRepository terceiros, ITerceirosUnitOfWork unitOfWork)
    : IIntegrationEventHandler<TerceiroVinculadoIntegrationEvent>
{
    public async Task Handle(TerceiroVinculadoIntegrationEvent evento, CancellationToken cancellationToken)
    {
        if (await terceiros.ObterAsync(evento.IdTerceiro, cancellationToken) is not Terceiro t)
        {
            return;
        }

        t.VincularConta(evento.IdUsuario, evento.ContaAtiva);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>O parceiro definiu a senha pelo convite: deixa de ser só convidado em todas as funerárias que o cadastraram.</summary>
internal sealed class ContaAtivadaIntegrationEventHandler(ITerceiroRepository terceiros, ITerceirosUnitOfWork unitOfWork)
    : IIntegrationEventHandler<ContaAtivadaIntegrationEvent>
{
    public async Task Handle(ContaAtivadaIntegrationEvent evento, CancellationToken cancellationToken)
    {
        IReadOnlyList<Terceiro> daConta = await terceiros.ListarDaContaEmTodasAsFunerariasAsync(evento.IdUsuario, cancellationToken);
        if (daConta.Count == 0)
        {
            return;
        }

        foreach (Terceiro t in daConta)
        {
            t.AtivarConta();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
