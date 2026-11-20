using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Cache;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Catalogo.Application.Abstractions;
using Funeraria.Modules.Catalogo.Domain.Itens;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Catalogo.Infrastructure.Database;

/// <summary>A lista pública do catálogo fica em cache (INF-19): toda gravação aqui invalida a da funerária.</summary>
public sealed class CatalogoDbContext(DbContextOptions<CatalogoDbContext> options, ITenantContext tenantContext, ICacheDaFuneraria cache)
    : ModuleDbContext(options, tenantContext), ICatalogoUnitOfWork
{
    public const string SchemaName = "catalogo";

    private readonly ITenantContext _tenantContext = tenantContext;

    public override string Schema => SchemaName;

    internal DbSet<Categoria> Categorias => Set<Categoria>();

    internal DbSet<ItemCatalogo> Itens => Set<ItemCatalogo>();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> funerarias = InvalidacaoAoSalvar.FunerariasAlteradas(this, _tenantContext);
        int gravados = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await InvalidacaoAoSalvar.InvalidarAsync(cache, funerarias, AreasCache.Catalogo, cancellationToken);
        return gravados;
    }

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogoDbContext).Assembly);
}
