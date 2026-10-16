using Funeraria.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.BuildingBlocks.Infrastructure.Database;

/// <summary>
/// Base do DbContext de cada módulo. Cada módulo tem o próprio DbContext, mapeado só para o seu schema,
/// o que impede pelo compilador que um módulo leia as tabelas de outro (INF-03).
/// </summary>
/// <remarks>
/// As convenções ficam em <see cref="ModelBuilderExtensions"/> para poderem ser usadas também pelo
/// DbContext da Identidade, que precisa herdar de <c>IdentityDbContext</c>.
/// </remarks>
public abstract class ModuleDbContext(DbContextOptions options, ITenantContext tenantContext)
    : DbContext(options), IModuleDbContext, IUnitOfWork
{
    /// <inheritdoc />
    public abstract string Schema { get; }

    /// <summary>Lido pelo filtro global a cada consulta. Sem tenant, as consultas não retornam nada.</summary>
    internal Guid? CurrentTenantId => tenantContext.TenantId;

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        ConfigureModel(modelBuilder);

        modelBuilder.ApplyOutbox();
        modelBuilder.ApplyTenantQueryFilters(this);
        modelBuilder.ApplyConcurrencyTokens();
        modelBuilder.ApplyAuditProperties();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);
    }

    /// <summary>Aplica as configurações das entidades do módulo (normalmente <c>ApplyConfigurationsFromAssembly</c>).</summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);
}
