using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Documentos.Application.Documentos;
using Funeraria.Modules.Documentos.Domain.Documentos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Documentos.Endpoints;

/// <summary>
/// Documentos do falecido (CER-01). A equipe interna anexa e remove; a equipe interna e o titular do contrato listam e
/// baixam por link temporário. O consultor não vê os documentos.
/// </summary>
internal sealed class DocumentosEndpoints : IEndpoint
{
    private const string Tag = "Documentos";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("beneficiarios/{id:guid}/documentos", async (Guid id, IFormFile? arquivo, [FromForm] string? tipo, ISender sender, CancellationToken ct) =>
            {
                await using Stream? conteudo = arquivo?.OpenReadStream();
                ArquivoEnviado? enviado = conteudo is null ? null : new ArquivoEnviado(arquivo!.FileName, conteudo);
                return (await sender.Send(new AnexarDocumentoCommand(id, tipo, enviado), ct))
                    .Match(d => Results.Created($"/api/v1/beneficiarios/{id}/documentos/{d.IdDocumento}/download", d));
            })
            // API com token no cabeçalho, sem cookie de sessão: não há CSRF a evitar aqui.
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("AnexarDocumento")
            .WithSummary("Anexa um documento do falecido (campos \"arquivo\" e \"tipo\")")
            .WithDescription("Tipos: DECLARACAO_OBITO, DOCUMENTO_FALECIDO, DOCUMENTO_DECLARANTE, COMPROVANTE_RESIDENCIA, CERTIDAO_CIVIL, OUTRO. "
                + "Arquivos PDF, JPEG ou PNG, até o tamanho da configuração (padrão 10 MB).")
            .Produces<DocumentoResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("beneficiarios/{id:guid}/documentos", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListarDocumentosQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("ListarDocumentos")
            .WithSummary("Documentos do falecido")
            .Produces<IReadOnlyList<DocumentoResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("beneficiarios/{id:guid}/documentos/{idDocumento:guid}/download", async (Guid id, Guid idDocumento, ISender sender, CancellationToken ct) =>
                (await sender.Send(new BaixarDocumentoQuery(id, idDocumento), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("BaixarDocumento")
            .WithSummary("Link temporário para baixar o documento")
            .Produces<LinkDocumentoResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapDelete("beneficiarios/{id:guid}/documentos/{idDocumento:guid}", async (Guid id, Guid idDocumento, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RemoverDocumentoCommand(id, idDocumento), ct)).Match(Results.NoContent))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("RemoverDocumento")
            .WithSummary("Remove o documento e o arquivo")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
