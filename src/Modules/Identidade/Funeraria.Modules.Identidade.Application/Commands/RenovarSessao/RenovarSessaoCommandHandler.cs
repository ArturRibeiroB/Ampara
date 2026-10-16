using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Application.Sessoes;
using Funeraria.Modules.Identidade.Domain;
using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Domain.Vinculos;

namespace Funeraria.Modules.Identidade.Application.Commands.RenovarSessao;

/// <summary>
/// Renova a sessão com rotação do refresh token (AUT-03):
/// <list type="bullet">
/// <item>Token desconhecido, expirado ou revogado: 401.</item>
/// <item>Token que já foi trocado sendo usado de novo: indica roubo; revoga todas as sessões do usuário.</item>
/// <item>Usuário desativado ou sem vínculo ativo: revoga o token e recusa.</item>
/// </list>
/// </summary>
public sealed class RenovarSessaoCommandHandler(
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    IUsuarios usuarios,
    IVinculoRepository vinculos,
    ITenantContext tenantContext,
    EmissorSessao emissor,
    IIdentidadeUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<RenovarSessaoCommand, SessaoResponse>
{
    public async ValueTask<Result<SessaoResponse>> Handle(RenovarSessaoCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return IdentidadeErros.SessaoInvalida;
        }

        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        RefreshToken? token = await refreshTokens.ObterPorHashAsync(tokens.Hash(command.RefreshToken), cancellationToken);
        if (token is null)
        {
            return IdentidadeErros.SessaoInvalida;
        }

        if (token.FoiRotacionado)
        {
            await RevogarTodasAsSessoesAsync(token.IdUsuario, agora, cancellationToken);
            return IdentidadeErros.SessaoInvalida;
        }

        // A sessão é de uma funerária: o refresh precisa vir do mesmo endereço em que foi feito o login.
        if (!token.EstaAtivo(agora) || token.IdTenant != tenantContext.TenantId)
        {
            return IdentidadeErros.SessaoInvalida;
        }

        UsuarioInfo? usuario = await usuarios.ObterAsync(token.IdUsuario, cancellationToken);
        if (usuario is not { Ativo: true, Bloqueado: false })
        {
            return await RecusarAsync(token, agora, usuario is null ? IdentidadeErros.SessaoInvalida : IdentidadeErros.UsuarioInativo, cancellationToken);
        }

        UsuarioVinculo? vinculo = null;
        if (token.IdTenant is Guid idTenant)
        {
            vinculo = await vinculos.ObterAsync(usuario.Id, idTenant, cancellationToken);
            if (vinculo is not { Ativo: true })
            {
                return await RecusarAsync(token, agora, IdentidadeErros.SemAcessoFuneraria, cancellationToken);
            }
        }
        else if (!usuario.Admin)
        {
            return await RecusarAsync(token, agora, IdentidadeErros.SessaoInvalida, cancellationToken);
        }

        SessaoResponse sessao = emissor.Emitir(usuario, vinculo, token);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return sessao;
    }

    private async Task<Error> RecusarAsync(RefreshToken token, DateTime agora, Error erro, CancellationToken cancellationToken)
    {
        token.Revogar(agora);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return erro;
    }

    private async Task RevogarTodasAsSessoesAsync(Guid idUsuario, DateTime agora, CancellationToken cancellationToken)
    {
        foreach (RefreshToken ativo in await refreshTokens.ListarAtivosDoUsuarioAsync(idUsuario, agora, cancellationToken))
        {
            ativo.Revogar(agora);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
