using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Tenants.Application.Commands.AtualizarConfiguracoes;
using Funeraria.Modules.Tenants.Application.Queries.ObterConfiguracoes;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Tenants.Endpoints;

/// <summary>Configurações da funerária (TEN-03). O upload do logo entra com o armazenamento de arquivos (INF-18).</summary>
internal sealed class ConfiguracoesEndpoints : IEndpoint
{
    private const string Tag = "Configurações da funerária";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tenant/configuracoes", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterIdentidadeVisualQuery(), ct)).Match(Results.Ok))
            .AllowAnonymous()
            .RequireTenant()
            .WithTags(Tag)
            .WithName("IdentidadeVisual")
            .WithSummary("Identidade visual da funerária do endereço acessado")
            .Produces<IdentidadeVisualResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        RouteGroupBuilder gestao = app.MapGroup("gestao/configuracoes")
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        gestao.MapGet(string.Empty, async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterConfiguracoesQuery(), ct)).Match(Results.Ok))
            .WithName("ObterConfiguracoes")
            .WithSummary("Configurações completas da funerária")
            .Produces<ConfiguracoesResponse>();

        gestao.MapPut(string.Empty, async (AtualizarConfiguracoesRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AtualizarConfiguracoesCommand(request.IdentidadeVisual, request.Parametros), ct))
                    .Match(Results.NoContent))
            .WithName("AtualizarConfiguracoes")
            .WithSummary("Atualiza identidade visual e parâmetros. Valem para novos contratos e próximas faturas")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
    }
}
