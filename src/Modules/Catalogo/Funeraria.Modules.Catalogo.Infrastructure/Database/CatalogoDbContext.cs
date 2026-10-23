using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Catalogo.Application.Abstractions;
using Funeraria.Modules.Catalogo.Domain.Itens;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Catalogo.Infrastructure.Database;

public sealed class CatalogoDbContext(DbContextOptions<CatalogoDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), ICatalogoUnitOfWork
{
    public const string SchemaName = "catalogo";

    public override string Schema => SchemaName;

    internal DbSet<Categoria> Categorias => Set<Categoria>();

    internal DbSet<ItemCatalogo> Itens => Set<ItemCatalogo>();

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogoDbContext).Assembly);
}
