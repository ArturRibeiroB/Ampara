using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Tenants.Contracts;

namespace Funeraria.Modules.Identidade.Application.Convites;

/// <summary>
/// Novo vínculo de equipe (gestor ou funcionário): quem ainda não tem senha recebe o convite com o link para
/// definir a senha; quem já tem conta na plataforma recebe só o aviso de acesso (DEC-02).
/// </summary>
/// <remarks>
/// Roda pelo outbox: se o envio falhar, o evento é processado de novo e um link novo é gerado. O link antigo,
/// que nunca chegou a ninguém, expira sozinho.
/// </remarks>
internal sealed class EnviarConviteDomainEventHandler(
    IUsuarios usuarios,
    ITenantsApi tenants,
    ITokenService tokens,
    ITokenSenhaRepository tokensSenha,
    IIdentidadeUnitOfWork unitOfWork,
    IConfiguracaoConvite convite,
    IEmailSender email,
    TimeProvider timeProvider)
    : IDomainEventHandler<VinculoCriadoDomainEvent>
{
    public async Task Handle(VinculoCriadoDomainEvent evento, CancellationToken cancellationToken)
    {
        // Cliente e terceiro têm o próprio fluxo de cadastro (CLI e TER).
        if (evento.Perfil is not (PerfilVinculo.Funeraria or PerfilVinculo.Funcionario))
        {
            return;
        }

        UsuarioInfo? usuario = await usuarios.ObterAsync(evento.IdUsuario, cancellationToken);
        FunerariaResumoDto? funeraria = await tenants.ObterPorIdAsync(evento.IdTenant, cancellationToken);
        if (usuario is null || funeraria is null)
        {
            return;
        }

        EmailMensagem mensagem;
        if (await usuarios.PossuiSenhaAsync(usuario.Id, cancellationToken))
        {
            mensagem = ModelosEmail.AcessoLiberado(usuario, funeraria, evento.Perfil, convite.UrlLogin(funeraria.Slug));
        }
        else
        {
            TokenOpaco token = tokens.GerarTokenOpaco();
            tokensSenha.Adicionar(TokenSenha.Criar(
                usuario.Id, FinalidadeTokenSenha.DefinirSenha, token.Hash, timeProvider.GetUtcNow().UtcDateTime, convite.ValidadeConvite));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            mensagem = ModelosEmail.Convite(
                usuario, funeraria, evento.Perfil, convite.UrlDefinirSenha(funeraria.Slug, token.Valor), convite.ValidadeConvite);
        }

        await email.EnviarAsync(mensagem, cancellationToken);
    }
}
