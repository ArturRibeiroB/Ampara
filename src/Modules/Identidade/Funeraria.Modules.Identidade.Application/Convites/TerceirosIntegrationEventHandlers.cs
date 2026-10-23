using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Contracts.IntegrationEvents;
using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Terceiros.Contracts.IntegrationEvents;

namespace Funeraria.Modules.Identidade.Application.Convites;

/// <summary>
/// Terceiro cadastrado (TER-01): cria a conta (ou reaproveita a existente, DEC-02) e o vínculo TERCEIRO. O e-mail sai pelo
/// <see cref="VinculoCriadoDomainEvent"/>, como no convite da equipe. Idempotente: o evento pode chegar mais de uma vez.
/// </summary>
internal sealed class ConvidarTerceiroIntegrationEventHandler(
    IUsuarios usuarios,
    IVinculoRepository vinculos,
    IIdentidadeUnitOfWork unitOfWork,
    IEventBus eventBus)
    : IIntegrationEventHandler<TerceiroCadastradoIntegrationEvent>
{
    public async Task Handle(TerceiroCadastradoIntegrationEvent evento, CancellationToken cancellationToken)
    {
        UsuarioInfo usuario = await usuarios.ObterPorEmailAsync(evento.Email, cancellationToken)
            ?? await usuarios.CriarConvidadoAsync(evento.Email, evento.Nome, cancellationToken);

        UsuarioVinculo? existente = await vinculos.ObterAsync(usuario.Id, evento.IdTenant, cancellationToken);
        if (existente is null)
        {
            vinculos.Adicionar(UsuarioVinculo.Criar(
                usuario.Id, evento.IdTenant, PerfilVinculo.Terceiro, idTerceiro: evento.IdTerceiro, tipoTerceiro: evento.Tipo));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else if (existente.IdTerceiro != evento.IdTerceiro)
        {
            // O e-mail ganhou outro acesso a esta funerária entre o cadastro e o convite: não dá para ligar.
            return;
        }

        bool contaAtiva = await usuarios.PossuiSenhaAsync(usuario.Id, cancellationToken);
        await eventBus.PublishAsync(
            new TerceiroVinculadoIntegrationEvent(evento.IdTenant, evento.IdTerceiro, usuario.Id, contaAtiva), cancellationToken);
    }
}

/// <summary>Terceiro desativado ou reativado na funerária: liga ou desliga o vínculo e derruba as sessões desta funerária.</summary>
internal sealed class AlterarVinculoTerceiroIntegrationEventHandler(
    IVinculoRepository vinculos,
    IRefreshTokenRepository refreshTokens,
    IIdentidadeUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<TerceiroStatusAlteradoIntegrationEvent>
{
    public async Task Handle(TerceiroStatusAlteradoIntegrationEvent evento, CancellationToken cancellationToken)
    {
        UsuarioVinculo? vinculo = await vinculos.ObterPorTerceiroAsync(evento.IdTenant, evento.IdTerceiro, cancellationToken);
        if (vinculo is null)
        {
            return;
        }

        vinculo.AlterarStatus(evento.Ativo);
        if (!evento.Ativo)
        {
            DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
            foreach (RefreshToken sessao in await refreshTokens.ListarAtivosDoUsuarioNaFunerariaAsync(vinculo.IdUsuario, evento.IdTenant, agora, cancellationToken))
            {
                sessao.Revogar(agora);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Senha definida pelo link do e-mail: avisa que a conta passou a ser usada (o Terceiros ativa o parceiro convidado).</summary>
internal sealed class PublicarContaAtivadaDomainEventHandler(IEventBus eventBus) : IDomainEventHandler<TokenSenhaUsadoDomainEvent>
{
    public Task Handle(TokenSenhaUsadoDomainEvent evento, CancellationToken cancellationToken) =>
        evento.Finalidade is FinalidadeTokenSenha.DefinirSenha or FinalidadeTokenSenha.RedefinirSenha
            ? eventBus.PublishAsync(new ContaAtivadaIntegrationEvent(evento.IdUsuario), cancellationToken)
            : Task.CompletedTask;
}
