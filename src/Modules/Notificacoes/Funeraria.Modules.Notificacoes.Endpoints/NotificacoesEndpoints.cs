using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Notificacoes.Application.Notificacoes;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Notificacoes.Endpoints;

/// <summary>
/// Central de notificações do usuário logado (NOT-02). Os avisos novos chegam também em tempo real pelo hub
/// <c>/hubs/notificacoes</c> (evento <c>notificacao</c>).
/// </summary>
internal sealed class NotificacoesEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder grupo = app.MapGroup("notificacoes")
            .RequireAuthorization()
            .WithTags("Notificações")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken ct,
                [FromQuery(Name = "nao_lidas")] bool naoLidas = false,
                int pagina = 1,
                int tamanho = PaginaRequest.TamanhoPadrao) =>
                (await sender.Send(new MinhasNotificacoesQuery(new PaginaRequest(pagina, tamanho), naoLidas), ct)).Match(Results.Ok))
            .WithName("MinhasNotificacoes")
            .WithSummary("Notificações do usuário na funerária acessada e da plataforma, com o total de não lidas")
            .Produces<NotificacoesResponse>();

        grupo.MapPatch("{id:guid}/lida", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new MarcarNotificacaoLidaCommand(id), ct)).Match(Results.NoContent))
            .WithName("MarcarNotificacaoLida")
            .WithSummary("Marca uma notificação como lida")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPatch("lidas", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new MarcarTodasLidasCommand(), ct)).Match(Results.NoContent))
            .WithName("MarcarTodasLidas")
            .WithSummary("Marca todas as notificações como lidas")
            .Produces(StatusCodes.Status204NoContent);
    }
}
