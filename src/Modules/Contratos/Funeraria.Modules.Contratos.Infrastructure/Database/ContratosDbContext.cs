using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Contratos.Application.Abstractions;
using Funeraria.Modules.Contratos.Domain.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Contratos.Infrastructure.Database;

public sealed class ContratosDbContext(DbContextOptions<ContratosDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), IContratosUnitOfWork
{
    public const string SchemaName = "contratos";

    public override string Schema => SchemaName;

    internal DbSet<Contrato> Contratos => Set<Contrato>();

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContratosDbContext).Assembly);
}
