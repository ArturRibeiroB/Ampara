using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Planos.Application.Abstractions;
using Funeraria.Modules.Planos.Domain.Planos;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Planos.Infrastructure.Database;

public sealed class PlanosDbContext(DbContextOptions<PlanosDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), IPlanosUnitOfWork
{
    public const string SchemaName = "planos";

    public override string Schema => SchemaName;

    internal DbSet<Plano> Planos => Set<Plano>();

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanosDbContext).Assembly);
}
