using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Documentos.Application.Certidoes;
using Funeraria.Modules.Documentos.Application.Documentos;
using Funeraria.Modules.Documentos.Domain.Certidoes;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Documentos.Endpoints;

/// <summary>
/// Certidão de óbito (CER-03, CER-04). A equipe interna pede, atribui e registra as etapas; o titular acompanha; o
/// despachante responsável registra as etapas e a emissão pelo portal do terceiro.
/// </summary>
internal sealed class CertidoesEndpoints : IEndpoint
{
    private const string Tag = "Documentos";
    private const string TagPortal = "Portal do terceiro";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("beneficiarios/{id:guid}/certidao", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SolicitarCertidaoCommand(id), ct)).Match(c => Results.Created($"/api/v1/beneficiarios/{id}/certidao", c)))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("SolicitarCertidao")
            .WithSummary("Pede o registro do óbito e a certidão")
            .WithDescription("Exige a declaração de óbito e os documentos do falecido e do declarante anexados. Uma solicitação por beneficiário. "
                + "No modo MANUAL, o responsável conduz no cartório e registra cada etapa.")
            .Produces<CertidaoResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("beneficiarios/{id:guid}/certidao/checklist", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ChecklistCertidaoQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("ChecklistCertidao")
            .WithSummary("Dados e documentos exigidos para o registro do óbito, com o prazo legal")
            .Produces<ChecklistCertidaoResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("beneficiarios/{id:guid}/certidao", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterCertidaoQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("ObterCertidao")
            .WithSummary("Andamento da certidão, com o histórico e o prazo")
            .Produces<CertidaoResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPatch("beneficiarios/{id:guid}/certidao/responsavel", async (Guid id, ResponsavelCertidaoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AtribuirResponsavelCertidaoCommand(id, r.Tipo, r.IdUsuario, r.IdTerceiro), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("AtribuirResponsavelCertidao")
            .WithSummary("Atribui a certidão a um funcionário ou a um despachante/cartório parceiro")
            .WithDescription("tipo = FUNCIONARIO (com id_usuario) ou TERCEIRO (com id_terceiro de um CARTORIO_DESPACHANTE). "
                + "O despachante recebe no portal só os dados e documentos desta solicitação.")
            .Produces<CertidaoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPatch("beneficiarios/{id:guid}/certidao/status", async (Guid id, StatusCertidaoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusCertidaoCommand(id, r.Status, r.Observacao, r.Protocolo), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("AlterarStatusCertidao")
            .WithSummary("Registra uma etapa: EM_PROCESSAMENTO (com o protocolo), REJEITADA ou ERRO (com a pendência) ou SOLICITADA (reenviar)")
            .Produces<CertidaoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("beneficiarios/{id:guid}/certidao/emitida", async (Guid id, IFormFile? arquivo, [FromForm(Name = "numero_matricula")] string? numeroMatricula, ISender sender, CancellationToken ct) =>
            {
                await using Stream? conteudo = arquivo?.OpenReadStream();
                ArquivoEnviado? enviado = conteudo is null ? null : new ArquivoEnviado(arquivo!.FileName, conteudo);
                return (await sender.Send(new RegistrarCertidaoEmitidaCommand(id, numeroMatricula, enviado), ct)).Match(Results.Ok);
            })
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("RegistrarCertidaoEmitida")
            .WithSummary("Registra a certidão emitida (campos \"arquivo\" e \"numero_matricula\") e avisa o titular")
            .Produces<CertidaoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        MapPortal(app);
    }

    private static void MapPortal(IEndpointRouteBuilder app)
    {
        app.MapPatch("terceiros/certidoes/{id:guid}/status", async (Guid id, StatusCertidaoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusCertidaoTerceiroCommand(id, r.Status, r.Observacao, r.Protocolo), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.TerceiroDespachante)
            .WithTags(TagPortal)
            .WithName("AlterarStatusCertidaoTerceiro")
            .WithSummary("O despachante registra uma etapa da certidão atribuída a ele")
            .Produces<CertidaoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("terceiros/certidoes/{id:guid}/emitida", async (Guid id, IFormFile? arquivo, [FromForm(Name = "numero_matricula")] string? numeroMatricula, ISender sender, CancellationToken ct) =>
            {
                await using Stream? conteudo = arquivo?.OpenReadStream();
                ArquivoEnviado? enviado = conteudo is null ? null : new ArquivoEnviado(arquivo!.FileName, conteudo);
                return (await sender.Send(new RegistrarCertidaoEmitidaTerceiroCommand(id, numeroMatricula, enviado), ct)).Match(Results.Ok);
            })
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .RequireAuthorization(Politicas.TerceiroDespachante)
            .WithTags(TagPortal)
            .WithName("RegistrarCertidaoEmitidaTerceiro")
            .WithSummary("O despachante envia o PDF da certidão e o número da matrícula")
            .Produces<CertidaoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("terceiros/certidoes/{id:guid}/documentos/{idDocumento:guid}/download", async (Guid id, Guid idDocumento, ISender sender, CancellationToken ct) =>
                (await sender.Send(new BaixarDocumentoCertidaoTerceiroQuery(id, idDocumento), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.TerceiroDespachante)
            .WithTags(TagPortal)
            .WithName("BaixarDocumentoCertidaoTerceiro")
            .WithSummary("Link temporário de um documento da certidão atribuída ao despachante")
            .Produces<LinkDocumentoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record ResponsavelCertidaoRequest(string? Tipo, Guid? IdUsuario, Guid? IdTerceiro);

public sealed record StatusCertidaoRequest(StatusCertidao? Status, string? Observacao, string? Protocolo);
