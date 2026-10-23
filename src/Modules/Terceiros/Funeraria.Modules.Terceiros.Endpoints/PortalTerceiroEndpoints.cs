using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Terceiros.Application.Atribuicoes;
using Funeraria.Modules.Terceiros.Application.Cadastro;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Terceiros.Endpoints;

/// <summary>
/// Portal do terceiro logado (TER-02). Ele vê só o que foi atribuído a ele e só os dados de que precisa para o serviço;
/// atribuição de outro terceiro dá 404.
/// </summary>
internal sealed class PortalTerceiroEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder portal = app.MapGroup("terceiros")
            .RequireAuthorization(Politicas.Terceiro)
            .WithTags("Portal do terceiro")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        portal.MapGet("me", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterMeuCadastroQuery(), ct)).Match(Results.Ok))
            .WithName("MeuCadastroTerceiro")
            .WithSummary("Cadastro do terceiro logado nesta funerária")
            .Produces<TerceiroResponse>();

        portal.MapGet("atribuicoes", async (ISender sender, CancellationToken ct, string? status = null) =>
                (await sender.Send(new ListarMinhasAtribuicoesQuery(status), ct)).Match(Results.Ok))
            .WithName("MinhasAtribuicoes")
            .WithSummary("Translados, certidões e pedidos atribuídos ao terceiro logado, numa lista só")
            .WithDescription("Status: PENDENTE, ACEITA, RECUSADA, EXPIRADA (pendente com o prazo vencido), CONCLUIDA ou CANCELADA.")
            .Produces<IReadOnlyList<AtribuicaoResponse>>()
            .ProducesValidationProblem();

        portal.MapPost("atribuicoes/{id:guid}/aceite", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AceitarAtribuicaoCommand(id), ct)).Match(Results.Ok))
            .WithName("AceitarAtribuicao")
            .WithSummary("Aceita o serviço atribuído")
            .Produces<AtribuicaoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        portal.MapPost("atribuicoes/{id:guid}/recusa", async (Guid id, RecusarAtribuicaoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RecusarAtribuicaoCommand(id, r.Motivo ?? string.Empty), ct)).Match(Results.Ok))
            .WithName("RecusarAtribuicao")
            .WithSummary("Recusa o serviço atribuído, com o motivo")
            .Produces<AtribuicaoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

public sealed record RecusarAtribuicaoRequest(string? Motivo);
