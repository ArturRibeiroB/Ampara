using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Funeral.Application.Orcamentos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Funeral.Endpoints;

/// <summary>Orçamento e cobrança dos itens adicionais (FUN-04).</summary>
internal sealed class OrcamentoEndpoints : IEndpoint
{
    private const string Tag = "Funeral";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("funerais/{id:guid}/orcamento", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GerarOrcamentoCommand(id), ct)).Match(o => Results.Created($"/api/v1/funerais/{id}/orcamento", o)))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("GerarOrcamentoFuneral")
            .WithSummary("Gera o orçamento com os itens adicionais ainda não aprovados")
            .WithDescription("Substitui o orçamento pendente. Incluir ou retirar itens também tira o efeito dele. 409 se não houver o que orçar.")
            .Produces<OrcamentoResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("funerais/{id:guid}/orcamento", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterOrcamentoQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("ObterOrcamentoFuneral")
            .WithSummary("Orçamento mais recente do funeral")
            .Produces<OrcamentoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("funerais/{id:guid}/orcamento/aprovar", async (Guid id, AprovarOrcamentoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AprovarOrcamentoCommand(id, r.IdOrcamento, r.NomeResponsavel), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("AprovarOrcamentoFuneral")
            .WithSummary("Registra a aprovação do responsável e emite a fatura")
            .WithDescription(
                "Pede o id do orçamento apresentado: se os itens mudaram depois dele, a resposta é 409. A fatura não suspende o plano nem gera multa e juros. " +
                "Com fatura_pendente, a aprovação valeu, mas a fatura não saiu: aprovar de novo tenta outra vez.")
            .Produces<OrcamentoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

public sealed record AprovarOrcamentoRequest(Guid? IdOrcamento, string? NomeResponsavel);
