using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Identidade.Infrastructure.Vinculos;

internal sealed class VinculoRepository(IdentidadeDbContext context) : IVinculoRepository
{
    public Task<UsuarioVinculo?> ObterAsync(Guid idUsuario, Guid idTenant, CancellationToken cancellationToken = default) =>
        context.Vinculos.FirstOrDefaultAsync(v => v.IdUsuario == idUsuario && v.IdTenant == idTenant, cancellationToken);

    public async Task<IReadOnlyList<UsuarioVinculo>> ListarAtivosDoUsuarioAsync(Guid idUsuario, CancellationToken cancellationToken = default) =>
        await context.Vinculos
            .AsNoTracking()
            .Where(v => v.IdUsuario == idUsuario && v.Ativo)
            .ToListAsync(cancellationToken);

    public Task<int> ContarGestoresAtivosAsync(Guid idTenant, CancellationToken cancellationToken = default) =>
        context.Vinculos.CountAsync(v => v.IdTenant == idTenant && v.Perfil == PerfilVinculo.Funeraria && v.Ativo, cancellationToken);

    public void Adicionar(UsuarioVinculo vinculo) => context.Vinculos.Add(vinculo);
}
