using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Planos.Application.Commands.AlterarStatusPlano;
using Funeraria.Modules.Planos.Application.Commands.AtualizarPlano;
using Funeraria.Modules.Planos.Application.Commands.CriarPlano;
using Funeraria.Modules.Planos.Application.Queries.ListarPlanos;
using Funeraria.Modules.Planos.Application.Queries.ObterPlano;
using Funeraria.Modules.Planos.Application.Commands.DefinirCoberturas;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Planos.Endpoints.Gestao;

/// <summary>Gestão dos planos pela funerária (PLA-01). Restrito ao perfil FUNERARIA (DEC-06).</summary>
internal sealed class GestaoPlanosEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup(Rotas.Gestao)
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Rotas.Tag)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet(string.Empty, async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListarPlanosQuery(SomenteAtivos: false), ct)).Match(Results.Ok))
            .WithName("GestaoListarPlanos")
            .WithSummary("Lista todos os planos da funerária, ativos e inativos")
            .Produces<IReadOnlyList<PlanoResumoResponse>>();

        group.MapGet("{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterPlanoQuery(id, SomenteAtivos: false), ct)).Match(Results.Ok))
            .WithName("GestaoObterPlano")
            .WithSummary("Detalhe de um plano da funerária")
            .Produces<PlanoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost(string.Empty, async (PlanoRequest request, ISender sender, CancellationToken ct) =>
            {
                var command = new CriarPlanoCommand(
                    request.Nome,
                    request.Descricao,
                    request.ValorMensal,
                    request.ValorAnual,
                    request.ValorDependenteMensal,
                    request.ValorDependenteAnual,
                    request.CarenciaDias,
                    request.LimiteDependentes,
                    request.PermiteSomenteDependente,
                    request.Configuracoes);

                return (await sender.Send(command, ct))
                    .Match(id => Results.Created($"/api/v1/{Rotas.Gestao}/{id}", new PlanoCriadoResponse(id)));
            })
            .WithName("CriarPlano")
            .WithSummary("Cria um plano")
            .Produces<PlanoCriadoResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("{id:guid}", async (Guid id, PlanoRequest request, ISender sender, CancellationToken ct) =>
            {
                var command = new AtualizarPlanoCommand(
                    id,
                    request.Nome,
                    request.Descricao,
                    request.ValorMensal,
                    request.ValorAnual,
                    request.ValorDependenteMensal,
                    request.ValorDependenteAnual,
                    request.CarenciaDias,
                    request.LimiteDependentes,
                    request.PermiteSomenteDependente,
                    request.Configuracoes);

                return (await sender.Send(command, ct)).Match(Results.NoContent);
            })
            .WithName("AtualizarPlano")
            .WithSummary("Edita um plano. Novo valor vale só para novos contratos")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("{id:guid}/status", async (Guid id, AlterarStatusPlanoRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusPlanoCommand(id, request.Ativo), ct)).Match(Results.NoContent))
            .WithName("AlterarStatusPlano")
            .WithSummary("Ativa ou desativa um plano")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("{id:guid}/coberturas", async (Guid id, DefinirCoberturasRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DefinirCoberturasCommand(id, request.Coberturas), ct)).Match(Results.NoContent))
            .WithName("DefinirCoberturasPlano")
            .WithSummary("Define os itens do catálogo que o plano cobre, trocando a lista inteira")
            .WithDescription("Cada item precisa estar ativo no catálogo. Lista vazia remove as coberturas (PLA-02).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
