using Funeraria.BuildingBlocks.Application.Abstractions;

namespace Funeraria.BuildingBlocks.Infrastructure.Tenancy;

/// <summary>
/// Implementação com escopo de requisição/job do <see cref="ITenantContext"/>.
/// Só o middleware de tenant, o outbox e os jobs devem chamar <see cref="Set"/>.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public void Set(Guid? tenantId) => TenantId = tenantId;
}
