using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Modules.Tenants.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Infrastructure.Funerarias;

internal sealed class FunerariaRepository(TenantsDbContext context) : IFunerariaRepository
{
    public Task<FunerariaEntity?> ObterPorIdAsync(Guid idTenant, CancellationToken cancellationToken = default) =>
        context.Funerarias.FirstOrDefaultAsync(f => f.Id == idTenant, cancellationToken);

    public Task<FunerariaEntity?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        string normalizado = slug.ToLowerInvariant();
        return context.Funerarias.FirstOrDefaultAsync(f => f.Slug == normalizado, cancellationToken);
    }

    public Task<bool> ExisteCnpjAsync(string cnpj, CancellationToken cancellationToken = default) =>
        context.Funerarias.AnyAsync(f => f.Cnpj == cnpj, cancellationToken);

    public Task<bool> ExisteSlugAsync(string slug, CancellationToken cancellationToken = default) =>
        context.Funerarias.AnyAsync(f => f.Slug == slug, cancellationToken);

    public void Adicionar(FunerariaEntity funeraria) => context.Funerarias.Add(funeraria);
}
