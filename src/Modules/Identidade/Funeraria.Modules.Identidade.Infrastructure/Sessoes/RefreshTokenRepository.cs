using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Identidade.Infrastructure.Sessoes;

internal sealed class RefreshTokenRepository(IdentidadeDbContext context) : IRefreshTokenRepository
{
    public Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListarAtivosDoUsuarioAsync(
        Guid idUsuario,
        DateTime agora,
        CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .Where(t => t.IdUsuario == idUsuario && t.RevogadoEm == null && t.ExpiraEm > agora)
            .ToListAsync(cancellationToken);

    public void Adicionar(RefreshToken refreshToken) => context.RefreshTokens.Add(refreshToken);
}
