using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Planos.Application.Queries.ListarPlanos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Planos.Endpoints.Publico;

internal sealed class ListarPlanosEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(Rotas.Publico, async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListarPlanosQuery(SomenteAtivos: true), ct)).Match(Results.Ok))
            .AllowAnonymous()
            .RequireTenant()
            .WithTags(Rotas.Tag)
            .WithName("ListarPlanos")
            .WithSummary("Lista os planos ativos da funerária")
            .Produces<IReadOnlyList<PlanoResumoResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}
