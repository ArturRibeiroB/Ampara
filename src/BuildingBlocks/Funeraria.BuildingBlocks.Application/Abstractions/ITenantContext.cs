namespace Funeraria.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Funerária (tenant) da execução atual. Na API vem do subdomínio/JWT (TEN-01);
/// em jobs e no processamento do outbox é definida a partir da mensagem.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    bool HasTenant => TenantId.HasValue;

    /// <summary>Id do tenant atual. Lança se não houver tenant, para não gravar dados soltos.</summary>
    Guid RequiredTenantId => TenantId
        ?? throw new InvalidOperationException("Nenhum tenant definido para esta operação.");
}
