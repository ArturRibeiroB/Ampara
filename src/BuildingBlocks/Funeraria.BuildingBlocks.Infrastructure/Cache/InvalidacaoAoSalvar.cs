using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.BuildingBlocks.Infrastructure.Cache;

/// <summary>
/// Para os DbContexts com dados em cache (INF-19): depois de gravar, invalida a área nas funerárias que tiveram mudança.
/// Assim nenhum caminho de escrita (comando, evento, seed) esquece de invalidar.
/// </summary>
public static class InvalidacaoAoSalvar
{
    /// <summary>Funerárias com alguma entidade incluída, alterada ou excluída. Chamar antes do SaveChanges.</summary>
    public static IReadOnlyList<Guid> FunerariasAlteradas(DbContext context, ITenantContext tenantContext)
    {
        var alteradas = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (alteradas.Count == 0)
        {
            return [];
        }

        HashSet<Guid> funerarias = [.. alteradas.Select(e => e.Entity).OfType<ITenantEntity>().Select(e => e.TenantId).Where(t => t != Guid.Empty)];
        if (tenantContext.TenantId is Guid atual)
        {
            funerarias.Add(atual);
        }

        return [.. funerarias];
    }

    public static async Task InvalidarAsync(ICacheDaFuneraria cache, IReadOnlyList<Guid> funerarias, string area, CancellationToken cancellationToken)
    {
        foreach (Guid funeraria in funerarias)
        {
            await cache.InvalidarAsync(funeraria, area, cancellationToken);
        }
    }
}
