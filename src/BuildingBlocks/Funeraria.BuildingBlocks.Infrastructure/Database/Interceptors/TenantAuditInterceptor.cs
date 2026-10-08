using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Funeraria.BuildingBlocks.Infrastructure.Database.Interceptors;

/// <summary>
/// Garante o isolamento por tenant na escrita (TEN-01) e preenche os campos de auditoria (INF-03):
/// <list type="bullet">
/// <item>INSERT: preenche id_tenant com o tenant atual e recusa gravar em outro tenant.</item>
/// <item>UPDATE: recusa trocar o id_tenant de uma entidade existente.</item>
/// </list>
/// </summary>
public sealed class TenantAuditInterceptor(ITenantContext tenantContext, ICurrentUser currentUser, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is ITenantEntity)
            {
                ProtectTenant(entry);
            }

            if (entry.Entity is IAuditableEntity)
            {
                StampAudit(entry, agora);
            }
        }
    }

    private void ProtectTenant(EntityEntry entry)
    {
        PropertyEntry tenant = entry.Property(nameof(ITenantEntity.TenantId));

        switch (entry.State)
        {
            case EntityState.Added:
                var valor = (Guid)tenant.CurrentValue!;
                if (valor == Guid.Empty)
                {
                    tenant.CurrentValue = tenantContext.RequiredTenantId;
                }
                else if (tenantContext.HasTenant && valor != tenantContext.TenantId)
                {
                    throw new InvalidOperationException(
                        $"Tentativa de gravar {entry.Metadata.ClrType.Name} em outra funerária.");
                }

                break;

            case EntityState.Modified when tenant.IsModified:
                throw new InvalidOperationException(
                    $"O tenant de {entry.Metadata.ClrType.Name} não pode ser alterado.");
        }
    }

    private void StampAudit(EntityEntry entry, DateTime agora)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                entry.Property(ModelBuilderExtensions.CreatedAt).CurrentValue = agora;
                entry.Property(ModelBuilderExtensions.CreatedBy).CurrentValue = currentUser.IdUsuario;
                break;

            case EntityState.Modified:
                entry.Property(ModelBuilderExtensions.UpdatedAt).CurrentValue = agora;
                break;
        }
    }
}
