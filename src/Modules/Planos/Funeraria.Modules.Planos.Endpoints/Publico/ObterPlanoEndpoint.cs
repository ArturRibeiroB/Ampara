using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Planos.Application.Queries.ObterPlano;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Planos.Endpoints.Publico;

internal sealed class ObterPlanoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet($"{Rotas.Publico}/{{id:guid}}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterPlanoQuery(id, SomenteAtivos: true), ct)).Match(Results.Ok))
            .AllowAnonymous()
            .RequireTenant()
            .WithTags(Rotas.Tag)
            .WithName("ObterPlano")
            .WithSummary("Detalhe de um plano ativo da funerária")
            .Produces<PlanoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
