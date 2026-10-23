namespace Funeraria.Modules.Identidade.Domain.Sessoes;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> ListarAtivosDoUsuarioAsync(Guid idUsuario, DateTime agora, CancellationToken cancellationToken = default);

    /// <summary>Sessões ativas do usuário numa funerária (AUT-08: desativar o vínculo derruba só essas).</summary>
    Task<IReadOnlyList<RefreshToken>> ListarAtivosDoUsuarioNaFunerariaAsync(
        Guid idUsuario,
        Guid idTenant,
        DateTime agora,
        CancellationToken cancellationToken = default);

    void Adicionar(RefreshToken refreshToken);
}
