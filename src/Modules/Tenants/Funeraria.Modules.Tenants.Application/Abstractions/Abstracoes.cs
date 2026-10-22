using Funeraria.BuildingBlocks.Application.Abstractions;

namespace Funeraria.Modules.Tenants.Application.Abstractions;

public interface ITenantsUnitOfWork : IUnitOfWork;

/// <summary>
/// Cache da resolução de tenant (subdomínio → funerária). Precisa ser limpo quando o status muda,
/// senão uma funerária suspensa continuaria acessível até o cache expirar.
/// </summary>
public interface ITenantCache
{
    void Invalidar(Guid idTenant, string slug);
}
