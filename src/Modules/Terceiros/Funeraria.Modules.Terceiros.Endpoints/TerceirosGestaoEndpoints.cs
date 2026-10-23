using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Terceiros.Application.Cadastro;
using Funeraria.Modules.Terceiros.Domain.Terceiros;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Terceiros.Endpoints;

/// <summary>Cadastro de terceiros pelo gestor da funerária (TER-01).</summary>
internal sealed class TerceirosGestaoEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder gestao = app.MapGroup("gestao/terceiros")
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags("Terceiros")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        gestao.MapPost(string.Empty, async (CadastrarTerceiroRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CadastrarTerceiroCommand(
                        r.Tipo, r.Email ?? string.Empty, r.NomeRazaoSocial ?? string.Empty, r.CpfCnpj ?? string.Empty,
                        r.Telefone, r.AreaAtuacao, r.CategoriasAtendidas), ct))
                    .Match(t => Results.Created($"/api/v1/gestao/terceiros/{t.IdTerceiro}", t)))
            .WithName("CadastrarTerceiro")
            .WithSummary("Cadastra um parceiro (FROTA, CARTORIO_DESPACHANTE ou FORNECEDOR) e envia o convite")
            .WithDescription("Quem ainda não tem conta recebe o link para definir a senha; quem já tem conta na plataforma só ganha "
                + "o acesso a esta funerária. Começa CONVIDADO e passa a ATIVO quando a conta tem senha.")
            .Produces<TerceiroResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        gestao.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken ct,
                string? tipo = null,
                string? status = null,
                int pagina = 1,
                int tamanho = PaginaRequest.TamanhoPadrao) =>
                (await sender.Send(new ListarTerceirosQuery(new PaginaRequest(pagina, tamanho), tipo, status), ct)).Match(Results.Ok))
            .WithName("ListarTerceiros")
            .WithSummary("Terceiros da funerária, por tipo e status")
            .Produces<PaginaResult<TerceiroResponse>>()
            .ProducesValidationProblem();

        gestao.MapGet("{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterTerceiroQuery(id), ct)).Match(Results.Ok))
            .WithName("ObterTerceiro")
            .WithSummary("Detalhe do terceiro")
            .Produces<TerceiroResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        gestao.MapPut("{id:guid}", async (Guid id, AtualizarTerceiroRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AtualizarTerceiroCommand(
                        id, r.NomeRazaoSocial ?? string.Empty, r.CpfCnpj ?? string.Empty, r.Telefone, r.AreaAtuacao, r.CategoriasAtendidas), ct))
                    .Match(Results.Ok))
            .WithName("AtualizarTerceiro")
            .WithSummary("Altera os dados do terceiro (tipo e e-mail não mudam)")
            .Produces<TerceiroResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        gestao.MapPatch("{id:guid}/status", async (Guid id, AlterarStatusTerceiroRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusTerceiroCommand(id, r.Ativo), ct)).Match(Results.NoContent))
            .WithName("AlterarStatusTerceiro")
            .WithSummary("Ativa ou desativa o terceiro nesta funerária")
            .WithDescription("Desativar não apaga o histórico, tira o acesso a esta funerária e impede novas atribuições.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record CadastrarTerceiroRequest(
    TipoTerceiro? Tipo,
    string? Email,
    string? NomeRazaoSocial,
    string? CpfCnpj,
    string? Telefone,
    AreaAtuacao? AreaAtuacao,
    IReadOnlyList<Guid>? CategoriasAtendidas);

public sealed record AtualizarTerceiroRequest(
    string? NomeRazaoSocial,
    string? CpfCnpj,
    string? Telefone,
    AreaAtuacao? AreaAtuacao,
    IReadOnlyList<Guid>? CategoriasAtendidas);

public sealed record AlterarStatusTerceiroRequest(bool? Ativo);
