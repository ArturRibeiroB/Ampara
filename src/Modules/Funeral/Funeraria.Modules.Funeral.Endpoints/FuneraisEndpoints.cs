using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Funeral.Application.Funerais;
using Funeraria.Modules.Funeral.Domain.Funerais;
using Funeraria.Modules.Funeral.Domain.Preferencias;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Funeral.Endpoints;

/// <summary>
/// Beneficiário (card Gerar Beneficiário), funeral (card Registrar Funeral), etapas (FUN-06) e acompanhamento (FUN-07).
/// Registra e muda etapas: FUNERARIA e FUNCIONARIO (DEC-06). A família (titular do contrato) acompanha.
/// </summary>
internal sealed class FuneraisEndpoints : IEndpoint
{
    private const string Tag = "Funeral";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("beneficiarios", async (RegistrarBeneficiarioRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RegistrarBeneficiarioCommand(
                    r.IdPessoaCoberta, r.DataFalecimento, r.LocalFalecimento, r.Responsavel?.Nome, r.Responsavel?.Telefone, r.Responsavel?.Parentesco), ct))
                .Match(b => Results.Created($"/api/v1/beneficiarios/{b.IdBeneficiario}", b)))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("RegistrarBeneficiario")
            .WithSummary("Registra o falecimento de uma pessoa coberta por contrato")
            .WithDescription("Use o id_pessoa_coberta do detalhe do contrato. A pessoa precisa poder usar o plano hoje (contrato ativo "
                + "e carência cumprida, CON-04); senão a resposta é 409 com o motivo.")
            .Produces<BeneficiarioResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("beneficiarios/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterBeneficiarioQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("ObterBeneficiario")
            .WithSummary("Detalhe do beneficiário (o titular vê os do próprio contrato)")
            .Produces<BeneficiarioResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("funerais", async (RegistrarFuneralRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RegistrarFuneralCommand(r.IdBeneficiario, r.DataHoraPrevista, r.Destinacao, r.Observacoes), ct))
                .Match(f => Results.Created($"/api/v1/funerais/{f.IdFuneral}", f)))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("RegistrarFuneral")
            .WithSummary("Abre o funeral do beneficiário, no status REGISTRADO")
            .WithDescription("A data do funeral não pode ser anterior ao falecimento. Local, itens, pedidos e translado são cadastrados depois.")
            .Produces<FuneralResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPatch("funerais/{id:guid}/status", async (Guid id, AlterarStatusFuneralRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusFuneralCommand(id, r.Status, r.Observacao), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("AlterarStatusFuneral")
            .WithSummary("Muda a etapa do funeral")
            .WithDescription("REGISTRADO → EM_PREPARACAO → VELORIO → CONCLUIDO, com CANCELADO a partir de qualquer etapa antes da conclusão. "
                + "Transição não permitida responde 400. Cada mudança entra no histórico e avisa a família.")
            .Produces<FuneralResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("funerais/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterFuneralQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("ObterFuneral")
            .WithSummary("Detalhe do funeral com o histórico de etapas (o titular vê os do próprio contrato)")
            .Produces<FuneralResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Equipe responsável (FUN-08).
        app.MapPut("funerais/{id:guid}/equipe/{idUsuario:guid}", async (Guid id, Guid idUsuario, AtribuirMembroRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AtribuirMembroFuneralCommand(id, idUsuario, r.Funcao), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("AtribuirMembroFuneral")
            .WithSummary("Coloca um gestor ou funcionário na equipe do funeral (ou troca a função dele)")
            .WithDescription("Funções: AGENTE_FUNERARIO, CERIMONIALISTA, MOTORISTA ou OUTRA. Funeral concluído ou cancelado não muda.")
            .Produces<FuneralResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapDelete("funerais/{id:guid}/equipe/{idUsuario:guid}", async (Guid id, Guid idUsuario, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RemoverMembroFuneralCommand(id, idUsuario), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("RemoverMembroFuneral")
            .WithSummary("Tira a pessoa da equipe do funeral")
            .Produces<FuneralResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("funerais/atribuidos", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new MeusFuneraisQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("MeusFunerais")
            .WithSummary("Funerais em andamento em que o usuário logado está na equipe")
            .Produces<IReadOnlyList<FuneralResumoResponse>>();

        app.MapGet("gestao/funerais", async (
                ISender sender,
                CancellationToken ct,
                string? status = null,
                [FromQuery(Name = "data_inicio")] DateOnly? dataInicio = null,
                [FromQuery(Name = "data_fim")] DateOnly? dataFim = null,
                int pagina = 1,
                int tamanho = PaginaRequest.TamanhoPadrao) =>
                (await sender.Send(new ListarFuneraisQuery(new PaginaRequest(pagina, tamanho), status, dataInicio, dataFim), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.Atendimento)
            .WithTags(Tag)
            .WithName("ListarFunerais")
            .WithSummary("Funerais da funerária, por status e período da cerimônia, em ordem de data")
            .Produces<PaginaResult<FuneralResumoResponse>>()
            .ProducesValidationProblem();
    }
}

public sealed record ResponsavelRequest(string? Nome, string? Telefone, string? Parentesco);

public sealed record RegistrarBeneficiarioRequest(
    Guid? IdPessoaCoberta, DateOnly? DataFalecimento, string? LocalFalecimento, ResponsavelRequest? Responsavel);

public sealed record RegistrarFuneralRequest(Guid? IdBeneficiario, DateTime? DataHoraPrevista, Destinacao? Destinacao, string? Observacoes);

public sealed record AtribuirMembroRequest(FuncaoEquipe? Funcao);

public sealed record AlterarStatusFuneralRequest(StatusFuneral? Status, string? Observacao);
