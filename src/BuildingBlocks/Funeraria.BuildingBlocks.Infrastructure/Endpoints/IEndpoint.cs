using Microsoft.AspNetCore.Routing;

namespace Funeraria.BuildingBlocks.Infrastructure.Endpoints;

/// <summary>
/// Um endpoint (ou grupo pequeno de endpoints) de uma feature. As implementações ficam no projeto
/// Endpoints de cada módulo e são descobertas e mapeadas pelo host.
/// </summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
