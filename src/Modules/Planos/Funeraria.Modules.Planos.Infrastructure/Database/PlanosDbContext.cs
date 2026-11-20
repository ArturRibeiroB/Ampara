using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Cache;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Planos.Application.Abstractions;
using Funeraria.Modules.Planos.Domain.Planos;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Planos.Infrastructure.Database;

/// <summary>A lista pública de planos fica em cache (INF-19): toda gravação aqui invalida a da funerária.</summary>
public sealed class PlanosDbContext(DbContextOptions<PlanosDbContext> options, ITenantContext tenantContext, ICacheDaFuneraria cache)
    : ModuleDbContext(options, tenantContext), IPlanosUnitOfWork
{
    public const string SchemaName = "planos";

    private readonly ITenantContext _tenantContext = tenantContext;

    public override string Schema => SchemaName;

    internal DbSet<Plano> Planos => Set<Plano>();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> funerarias = InvalidacaoAoSalvar.FunerariasAlteradas(this, _tenantContext);
        int gravados = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await InvalidacaoAoSalvar.InvalidarAsync(cache, funerarias, AreasCache.Planos, cancellationToken);
        return gravados;
    }

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlanosDbContext).Assembly);
}
