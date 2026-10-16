using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Domain.Vinculos;

namespace Funeraria.Modules.Identidade.Application.Sessoes;

/// <summary>
/// Monta uma sessão: access token com o perfil do vínculo e refresh token novo (ou rotacionado).
/// Usado pelo login e pela renovação, para os dois emitirem exatamente as mesmas claims.
/// </summary>
public sealed class EmissorSessao(ITokenService tokens, IRefreshTokenRepository refreshTokens, TimeProvider timeProvider)
{
    /// <param name="usuario">Conta autenticada.</param>
    /// <param name="vinculo">Vínculo com a funerária da sessão. Nulo só para o ADMIN.</param>
    /// <param name="anterior">Refresh token sendo trocado, na renovação. Nulo no login.</param>
    public SessaoResponse Emitir(UsuarioInfo usuario, UsuarioVinculo? vinculo, RefreshToken? anterior)
    {
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        string perfil = vinculo is null ? Perfis.Admin : vinculo.Perfil.ParaClaim();

        AccessToken access = tokens.GerarAccessToken(new DadosToken(
            usuario.Id,
            usuario.Email,
            usuario.Nome,
            perfil,
            vinculo?.IdTenant,
            vinculo?.IdCliente,
            vinculo?.IdTerceiro,
            vinculo?.TipoTerceiro));

        RefreshTokenGerado gerado = tokens.GerarRefreshToken();
        RefreshToken refresh = anterior is null
            ? RefreshToken.Criar(usuario.Id, vinculo?.IdTenant, gerado.Hash, agora, tokens.DuracaoRefreshToken)
            : anterior.Rotacionar(gerado.Hash, agora, tokens.DuracaoRefreshToken);

        refreshTokens.Adicionar(refresh);

        return new SessaoResponse(
            access.Token,
            access.ExpiraEm,
            new UsuarioSessao(usuario.Id, usuario.Nome, perfil),
            gerado.Valor,
            refresh.ExpiraEm);
    }
}

/// <summary>
/// Resultado do login e da renovação. O refresh token não vai no corpo da resposta:
/// o endpoint o coloca num cookie httpOnly (AUT-03).
/// </summary>
public sealed record SessaoResponse(
    string AccessToken,
    DateTime ExpiraEm,
    UsuarioSessao Usuario,
    string RefreshToken,
    DateTime RefreshExpiraEm);

public sealed record UsuarioSessao(Guid Id, string Nome, string Role);

public static class PerfilVinculoExtensions
{
    public static string ParaClaim(this PerfilVinculo perfil) => perfil switch
    {
        PerfilVinculo.Funeraria => Perfis.Funeraria,
        PerfilVinculo.Funcionario => Perfis.Funcionario,
        PerfilVinculo.Cliente => Perfis.Cliente,
        PerfilVinculo.Terceiro => Perfis.Terceiro,
        _ => throw new ArgumentOutOfRangeException(nameof(perfil), perfil, null)
    };
}
