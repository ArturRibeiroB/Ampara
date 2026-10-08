using System.Reflection;
using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Funeraria.BuildingBlocks.Infrastructure.Database;

public static class ModelBuilderExtensions
{
    public const string TenantColumn = "id_tenant";

    internal const string CreatedAt = "CreatedAt";
    internal const string UpdatedAt = "UpdatedAt";
    internal const string CreatedBy = "CreatedBy";

    private static readonly MethodInfo ApplyTenantFilterMethod = typeof(ModelBuilderExtensions)
        .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Filtro global por tenant em toda entidade <see cref="ITenantEntity"/> (TEN-01).
    /// Para desligar, use <c>IgnoreQueryFilters()</c> de forma explícita e registre na auditoria.
    /// </summary>
    public static ModelBuilder ApplyTenantQueryFilters(this ModelBuilder modelBuilder, ModuleDbContext context)
    {
        foreach (IMutableEntityType entityType in RootEntityTypes(modelBuilder)
                     .Where(e => typeof(ITenantEntity).IsAssignableFrom(e.ClrType)))
        {
            ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(null, [modelBuilder, context]);
        }

        return modelBuilder;
    }

    /// <summary>Coluna de sistema xmin do PostgreSQL como token de concorrência otimista dos agregados.</summary>
    public static ModelBuilder ApplyConcurrencyTokens(this ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in RootEntityTypes(modelBuilder)
                     .Where(e => IsAggregateRoot(e.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Property<uint>("xmin").IsRowVersion();
        }

        return modelBuilder;
    }

    /// <summary>Colunas created_at, updated_at e created_by como propriedades de sombra (INF-03).</summary>
    public static ModelBuilder ApplyAuditProperties(this ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in RootEntityTypes(modelBuilder)
                     .Where(e => typeof(IAuditableEntity).IsAssignableFrom(e.ClrType)))
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            entity.Property<DateTime>(CreatedAt);
            entity.Property<DateTime?>(UpdatedAt);
            entity.Property<Guid?>(CreatedBy);
        }

        return modelBuilder;
    }

    private static void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder, ModuleDbContext context)
        where TEntity : class, ITenantEntity
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.Property(e => e.TenantId).HasColumnName(TenantColumn);
        entity.HasIndex(e => e.TenantId);
        entity.HasQueryFilter(e => e.TenantId == context.CurrentTenantId);
    }

    private static IEnumerable<IMutableEntityType> RootEntityTypes(ModelBuilder modelBuilder) =>
        modelBuilder.Model.GetEntityTypes()
            .Where(e => e.BaseType is null && !e.IsOwned() && e.ClrType != typeof(Dictionary<string, object>))
            .ToList();

    private static bool IsAggregateRoot(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AggregateRoot<>))
            {
                return true;
            }
        }

        return false;
    }
}
