using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Funeral.Application.Itens;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Funeral.Endpoints;

/// <summary>Personalização do funeral (FUN-03): a equipe monta os itens com a família; o titular acompanha.</summary>
internal sealed class ItensFuneralEndpoints : IEndpoint
{
    private const string Tag = "Funeral";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("funerais/{id:guid}/itens", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ItensFuneralQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("ItensFuneral")
            .WithSummary("Itens do funeral: cobertos pelo plano, adicionais e total adicional")
            .WithDescription("A cobertura usa as quantidades do plano do contrato. Num item coberto com variação mais cara, a família paga só a diferença.")
            .Produces<ComposicaoItensResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("funerais/{id:guid}/itens", async (Guid id, ItemFuneralRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AdicionarItemFuneralCommand(id, r.IdItemCatalogo, r.IdVariacao, r.Quantidade), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("AdicionarItemFuneral")
            .WithSummary("Inclui um item do catálogo no funeral")
            .WithDescription("Item com estoque controlado baixa a quantidade na hora (409 se não houver). Funeral concluído ou cancelado não muda.")
            .Produces<ComposicaoItensResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapDelete("funerais/{id:guid}/itens/{idItem:guid}", async (Guid id, Guid idItem, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RemoverItemFuneralCommand(id, idItem), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("RemoverItemFuneral")
            .WithSummary("Tira o item do funeral e devolve o estoque")
            .Produces<ComposicaoItensResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("funerais/{id:guid}/itens/importar-preferencias", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ImportarPreferenciasCommand(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("ImportarPreferenciasFuneral")
            .WithSummary("Traz os itens das preferências registradas em vida pelo falecido")
            .WithDescription("Itens que já estão no funeral, fora do catálogo ou sem estoque ficam de fora e vêm em ignorados, com o motivo.")
            .Produces<ImportacaoPreferenciasResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

public sealed record ItemFuneralRequest(Guid? IdItemCatalogo, Guid? IdVariacao, int? Quantidade);
