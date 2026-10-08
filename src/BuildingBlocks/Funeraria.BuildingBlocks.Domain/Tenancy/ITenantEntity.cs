namespace Funeraria.BuildingBlocks.Domain.Tenancy;

/// <summary>
/// Entidade que pertence a uma funerária. O EF Core aplica o filtro global por <see cref="TenantId"/>
/// e o interceptor impede gravar em outro tenant ou trocar o tenant depois de criada (TEN-01).
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; }
}
