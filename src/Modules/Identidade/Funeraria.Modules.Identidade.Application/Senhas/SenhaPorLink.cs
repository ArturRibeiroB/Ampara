using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Domain.Sessoes;

namespace Funeraria.Modules.Identidade.Application.Senhas;

/// <summary>
/// Define a senha pelo token de um link enviado por e-mail: convite (TEN-02, AUT-07) ou "esqueci minha senha" (AUT-05).
/// O link vale uma vez, expira e só serve para a finalidade com que foi gerado.
/// </summary>
public sealed class SenhaPorLink(
    ITokenSenhaRepository tokensSenha,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    IUsuarios usuarios,
    IIdentidadeUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Result> DefinirAsync(
        string valorToken,
        string senha,
        FinalidadeTokenSenha finalidade,
        Error linkInvalido,
        CancellationToken cancellationToken)
    {
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        TokenSenha? token = await tokensSenha.ObterPorHashAsync(tokens.Hash(valorToken), cancellationToken);
        if (token is null || !token.EstaValido(agora) || token.Finalidade != finalidade)
        {
            return linkInvalido;
        }

        // Senha fraca não consome o link: a pessoa corrige e tenta de novo.
        Result definida = await usuarios.DefinirSenhaAsync(token.IdUsuario, senha, cancellationToken);
        if (definida.IsFailure)
        {
            return definida;
        }

        token.MarcarUsado(agora);

        // Senha trocada derruba as sessões abertas: se alguém tinha a senha antiga, perde o acesso.
        foreach (RefreshToken sessao in await refreshTokens.ListarAtivosDoUsuarioAsync(token.IdUsuario, agora, cancellationToken))
        {
            sessao.Revogar(agora);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
