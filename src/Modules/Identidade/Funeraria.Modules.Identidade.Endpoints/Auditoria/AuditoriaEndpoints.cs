using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Identidade.Application.Auditoria;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Identidade.Endpoints.Auditoria;

/// <summary>Consulta da auditoria (AUT-09): o gestor vê a da funerária; o ADMIN, a da plataforma (DEC-06).</summary>
internal sealed class AuditoriaEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("gestao/auditoria", (ISender sender, CancellationToken ct, [AsParameters] FiltroAuditoria f) => ConsultarAsync(sender, f, false, ct))
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags("Auditoria")
            .WithName("AuditoriaFuneraria")
            .WithSummary("Quem fez o quê na funerária, das ações mais recentes para as mais antigas")
            .Produces<PaginaResult<RegistroAuditoriaResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapGet("admin/auditoria", (ISender sender, CancellationToken ct, [AsParameters] FiltroAuditoria f) => ConsultarAsync(sender, f, true, ct))
            .RequireAuthorization(Politicas.Plataforma)
            .WithTags("Auditoria")
            .WithName("AuditoriaPlataforma")
            .WithSummary("Ações feitas pelo ADMIN no nível da plataforma")
            .Produces<PaginaResult<RegistroAuditoriaResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> ConsultarAsync(ISender sender, FiltroAuditoria f, bool plataforma, CancellationToken ct) =>
        (await sender.Send(new ConsultarAuditoriaQuery(
            new PaginaRequest(f.Pagina ?? 1, f.Tamanho ?? PaginaRequest.TamanhoPadrao),
            plataforma, f.Acao, f.Entidade, f.IdEntidade, f.IdUsuario, f.De, f.Ate), ct)).Match(Results.Ok);
}

/// <summary>Filtros de GET /gestao/auditoria e /admin/auditoria.</summary>
internal sealed record FiltroAuditoria(
    [property: FromQuery(Name = "acao")] string? Acao,
    [property: FromQuery(Name = "entidade")] string? Entidade,
    [property: FromQuery(Name = "id_entidade")] Guid? IdEntidade,
    [property: FromQuery(Name = "id_usuario")] Guid? IdUsuario,
    [property: FromQuery(Name = "de")] DateTime? De,
    [property: FromQuery(Name = "ate")] DateTime? Ate,
    [property: FromQuery(Name = "pagina")] int? Pagina,
    [property: FromQuery(Name = "tamanho")] int? Tamanho);
