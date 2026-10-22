using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Tenants.Contracts.IntegrationEvents;

namespace Funeraria.Modules.Identidade.Application.Convites;

/// <summary>
/// Funerária cadastrada: cria a conta do gestor (ou reaproveita a existente, DEC-02) e o vínculo FUNERARIA (TEN-02).
/// O e-mail sai depois, pelo <see cref="VinculoCriadoDomainEvent"/>. Idempotente: o evento pode chegar duas vezes.
/// </summary>
internal sealed class ConvidarGestorIntegrationEventHandler(
    IUsuarios usuarios,
    IVinculoRepository vinculos,
    IIdentidadeUnitOfWork unitOfWork)
    : IIntegrationEventHandler<FunerariaCriadaIntegrationEvent>
{
    public async Task Handle(FunerariaCriadaIntegrationEvent evento, CancellationToken cancellationToken)
    {
        UsuarioInfo gestor = await usuarios.ObterPorEmailAsync(evento.GestorEmail, cancellationToken)
            ?? await usuarios.CriarConvidadoAsync(evento.GestorEmail, evento.GestorNome, cancellationToken);

        if (await vinculos.ObterAsync(gestor.Id, evento.IdTenant, cancellationToken) is not null)
        {
            return;
        }

        vinculos.Adicionar(UsuarioVinculo.Criar(gestor.Id, evento.IdTenant, PerfilVinculo.Funeraria));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
