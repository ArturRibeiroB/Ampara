using Funeraria.BuildingBlocks.Application.Abstractions;

namespace Funeraria.Modules.Tenants.Application.Abstractions;

public interface ITenantsUnitOfWork : IUnitOfWork;

/// <summary>
/// Cache da resolução de tenant (subdomínio → funerária). Precisa ser limpo quando o status muda,
/// senão uma funerária suspensa continuaria acessível até o cache expirar.
/// </summary>
/// <summary>Endereços públicos servidos pela API, usados no frontend e nos e-mails.</summary>
public interface ILinksTenants
{
    /// <summary>Endereço do logo. A versão muda a cada upload, para navegadores e e-mails não mostrarem o antigo.</summary>
    string UrlLogo(string slug, string versao);
}

public interface ITenantCache
{
    void Invalidar(Guid idTenant, string slug);
}
