using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Tenants.Application.Commands.AtualizarConfiguracoes;
using Funeraria.Modules.Tenants.Application.Commands.Logo;
using Funeraria.Modules.Tenants.Application.Queries.ObterLogo;
using Funeraria.Modules.Tenants.Application.Queries.ObterConfiguracoes;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Tenants.Endpoints;

/// <summary>Configurações e logo da funerária (TEN-03). O logo fica no armazenamento de arquivos (INF-18).</summary>
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

        app.MapGet("funerarias/{slug}/logo", async (string slug, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                Result<ArquivoLido> logo = await sender.Send(new ObterLogoQuery(slug), ct);
                if (logo.IsFailure)
                {
                    return ApiResults.Problem(logo);
                }

                // O endereço muda a cada upload (?v=), então dá para guardar em cache.
                http.Response.Headers.CacheControl = "public, max-age=86400";
                return Results.Stream(logo.Value.Conteudo, logo.Value.ContentType);
            })
            .AllowAnonymous()
            .WithTags(Tag)
            .WithName("LogoFuneraria")
            .WithSummary("Logo da funerária, para telas e e-mails")
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .ProducesProblem(StatusCodes.Status404NotFound);

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

        gestao.MapPost("logo", async (IFormFile? arquivo, ISender sender, CancellationToken ct) =>
            {
                await using Stream? conteudo = arquivo?.OpenReadStream();
                ArquivoEnviado? enviado = conteudo is null ? null : new ArquivoEnviado(arquivo!.FileName, conteudo);
                return (await sender.Send(new EnviarLogoCommand(enviado), ct)).Match(Results.Ok);
            })
            // API com token no cabeçalho, sem cookie de sessão: não há CSRF a evitar aqui.
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithName("EnviarLogo")
            .WithSummary("Envia o logo (campo \"arquivo\"): PNG, JPEG ou WEBP de até 2 MB")
            .WithDescription("Substitui o logo atual. O tipo é conferido pelo conteúdo do arquivo, não pela extensão.")
            .Produces<LogoResponse>()
            .ProducesValidationProblem();

        gestao.MapDelete("logo", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new RemoverLogoCommand(), ct)).Match(Results.NoContent))
            .WithName("RemoverLogo")
            .WithSummary("Remove o logo")
            .Produces(StatusCodes.Status204NoContent);
    }
}
