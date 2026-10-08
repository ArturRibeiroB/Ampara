using Funeraria.Modules.Tenants.Contracts;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Modules.Tenants.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Funeraria.Modules.Tenants.Infrastructure.Funerarias;

internal sealed class TenantsApi(TenantsDbContext context, IMemoryCache cache) : ITenantsApi
{
    private static readonly TimeSpan Duracao = TimeSpan.FromMinutes(5);

    public async Task<FunerariaResumoDto?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        string chave = $"tenants:slug:{slug.ToLowerInvariant()}";
        if (cache.TryGetValue(chave, out FunerariaResumoDto? cached))
        {
            return cached;
        }

        FunerariaResumoDto? funeraria = await context.Funerarias
            .AsNoTracking()
            .Where(f => f.Slug == slug.ToLowerInvariant())
            .Select(f => new FunerariaResumoDto(f.Id, f.Slug, f.NomeFantasia, f.Status == StatusFuneraria.Suspensa))
            .FirstOrDefaultAsync(cancellationToken);

        if (funeraria is not null)
        {
            cache.Set(chave, funeraria, Duracao);
        }

        return funeraria;
    }
}
