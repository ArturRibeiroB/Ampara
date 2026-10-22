using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Tenants.Application.Commands.AlterarStatusFuneraria;
using Funeraria.Modules.Tenants.Application.Commands.CadastrarFuneraria;
using Funeraria.Modules.Tenants.Application.Queries.ListarFunerarias;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Tenants.Endpoints;

/// <summary>Cadastro e gestão das funerárias (TEN-02).</summary>
internal sealed class FunerariasEndpoints : IEndpoint
{
    private const string Tag = "Funerárias";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("onboarding/funerarias", async (CadastrarFunerariaRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(Comando(request, OrigemCadastro.Auto), ct))
                    .Match(f => Results.Created((string?)null, f)))
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Onboarding)
            .WithTags(Tag)
            .WithName("AutoCadastroFuneraria")
            .WithSummary("Auto-cadastro de funerária pela landing")
            .WithDescription("A funerária nasce PENDENTE e o gestor recebe por e-mail o link para definir a senha.")
            .Produces<FunerariaCadastradaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        RouteGroupBuilder admin = app.MapGroup("admin/tenants")
            .RequireAuthorization(Politicas.Plataforma)
            .WithTags(Tag)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapPost(string.Empty, async (CadastrarFunerariaRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(Comando(request, OrigemCadastro.Admin), ct))
                    .Match(f => Results.Created($"/api/v1/admin/tenants/{f.IdTenant}", f)))
            .WithName("CadastrarFuneraria")
            .WithSummary("ADMIN cadastra uma funerária")
            .Produces<FunerariaCadastradaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken ct,
                int pagina = 1,
                int tamanho = PaginaRequest.TamanhoPadrao,
                string? busca = null,
                StatusFuneraria? status = null) =>
                (await sender.Send(new ListarFunerariasQuery(new PaginaRequest(pagina, tamanho), busca, status), ct)).Match(Results.Ok))
            .WithName("ListarFunerarias")
            .WithSummary("Lista as funerárias da plataforma")
            .Produces<PaginaResult<FunerariaListaResponse>>();

        admin.MapPatch("{id:guid}/status", async (Guid id, AlterarStatusFunerariaRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusFunerariaCommand(id, request.Status), ct)).Match(Results.NoContent))
            .WithName("AlterarStatusFuneraria")
            .WithSummary("Aprova, suspende ou reativa uma funerária")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static CadastrarFunerariaCommand Comando(CadastrarFunerariaRequest r, OrigemCadastro origem) => new(
        r.NomeFantasia ?? string.Empty,
        r.RazaoSocial ?? string.Empty,
        r.Cnpj ?? string.Empty,
        r.Slug ?? string.Empty,
        r.EmailContato ?? string.Empty,
        r.Telefone,
        r.Gestor?.Nome ?? string.Empty,
        r.Gestor?.Email ?? string.Empty,
        origem);
}
