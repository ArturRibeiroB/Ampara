using Funeraria.Modules.Planos.Domain.Planos;
using Funeraria.Modules.Planos.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Planos.Infrastructure.Planos;

/// <summary>Acesso aos planos pelo EF Core. O filtro global já restringe ao tenant atual (TEN-01).</summary>
internal sealed class PlanoRepository(PlanosDbContext context) : IPlanoRepository
{
    public Task<Plano?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Planos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> ExisteComNomeAsync(string nome, Guid? exceto = null, CancellationToken cancellationToken = default) =>
        context.Planos.AnyAsync(p => p.Nome == nome && p.Id != exceto, cancellationToken);

    public void Adicionar(Plano plano) => context.Planos.Add(plano);
}
