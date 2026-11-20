using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Funeral.Contracts.IntegrationEvents;
using Funeraria.Modules.Translado.Application.Translados;
using Funeraria.Modules.Terceiros.Contracts;
using Funeraria.Modules.Translado.Domain.Translados;

namespace Funeraria.Modules.Translado.Application.Retencao;

/// <summary>Falecido anonimizado (SEG-01): os translados dele perdem nomes, contatos e endereços completos. Idempotente.</summary>
internal sealed class AnonimizarTransladosDoFalecido(
    ITransladoRepository translados,
    ITransladoUnitOfWork unitOfWork,
    ITerceirosAtribuicoes atribuicoes,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<FalecidoAnonimizadoIntegrationEvent>
{
    public async Task Handle(FalecidoAnonimizadoIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        IReadOnlyList<Domain.Translados.Translado> doFalecido = await translados.ListarDoBeneficiarioAsync(integrationEvent.IdBeneficiario, cancellationToken);
        foreach (Domain.Translados.Translado translado in doFalecido)
        {
            translado.Anonimizar(agora);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        // O que a empresa de translado vê no portal também (TER-02).
        await atribuicoes.AnonimizarResumosAsync([.. doFalecido.Select(t => t.Id)], cancellationToken);
    }
}
