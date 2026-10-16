namespace Funeraria.Modules.Identidade.Domain.Sessoes;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> ListarAtivosDoUsuarioAsync(Guid idUsuario, DateTime agora, CancellationToken cancellationToken = default);

    void Adicionar(RefreshToken refreshToken);
}
