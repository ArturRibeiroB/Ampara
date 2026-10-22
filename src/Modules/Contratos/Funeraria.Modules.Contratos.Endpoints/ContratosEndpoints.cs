using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Contratos.Application;
using Funeraria.Modules.Contratos.Application.Commands.CriarContrato;
using Funeraria.Modules.Contratos.Application.Queries;
using Funeraria.Modules.Contratos.Domain.Contratos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Contratos.Endpoints;

/// <summary>
/// Contratação (card Criar Contrato) e consulta (CON-01). O cliente contrata para si, com o titular vindo do token; a equipe da funerária
/// (FUNERARIA e FUNCIONARIO, DEC-06) contrata para um cliente informado.
/// </summary>
internal sealed class ContratosEndpoints : IEndpoint
{
    private const string Tag = "Contratos";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("contratos", async (ContratarRequest r, ICurrentUser usuario, ISender sender, CancellationToken ct) =>
                Criado(await sender.Send(
                    new CriarContratoCommand(usuario.IdCliente, r.IdPlano, r.TipoCobertura, r.Dependentes, r.Periodicidade, r.DiaVencimento), ct)))
            .RequireAuthorization(Politicas.Cliente)
            .WithTags(Tag)
            .WithName("Contratar")
            .WithSummary("O cliente logado contrata um plano para si e, se quiser, para os seus dependentes")
            .WithDescription("Coberturas: TITULAR, TITULAR_E_DEPENDENTES ou SOMENTE_DEPENDENTES (se o plano permitir). "
                + "O contrato começa A_PAGAR e é ativado com o pagamento da primeira cobrança.")
            .Produces<ContratoResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("gestao/contratos", async (ContratarParaClienteRequest r, ISender sender, CancellationToken ct) =>
                Criado(await sender.Send(
                    new CriarContratoCommand(r.IdCliente, r.IdPlano, r.TipoCobertura, r.Dependentes, r.Periodicidade, r.DiaVencimento), ct)))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("ContratarParaCliente")
            .WithSummary("A equipe da funerária cria o contrato de um cliente")
            .Produces<ContratoResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        MapConsultas(app);
    }

    private static void MapConsultas(IEndpointRouteBuilder app)
    {
        app.MapGet("contratos", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListarMeusContratosQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.Cliente)
            .WithTags(Tag)
            .WithName("MeusContratos")
            .WithSummary("Contratos do cliente logado, do mais recente para o mais antigo")
            .Produces<IReadOnlyList<ContratoResumoResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapGet("contratos/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterContratoQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("ObterContrato")
            .WithSummary("Detalhe do contrato com plano e pessoas cobertas")
            .WithDescription("A equipe vê qualquer contrato da funerária; o cliente, só os próprios (os outros dão 404).")
            .Produces<ContratoDetalheResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("gestao/contratos", async (
                ISender sender,
                CancellationToken ct,
                string? status = null,
                [FromQuery(Name = "id_plano")] Guid? idPlano = null,
                string? cpf = null,
                int pagina = 1,
                int tamanho = PaginaRequest.TamanhoPadrao,
                string? ordenarPor = null,
                string? direcao = null) =>
                (await sender.Send(new BuscarContratosQuery(
                    new PaginaRequest(pagina, tamanho), status, idPlano, cpf, new OrdenacaoRequest(ordenarPor, direcao)), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("BuscarContratos")
            .WithSummary("Busca contratos da funerária por status, plano e CPF do titular")
            .WithDescription("Ordenação: ordenarPor = data_contratacao (padrão, mais recentes primeiro), valor_total, status ou plano; "
                + "direcao = asc ou desc.")
            .Produces<PaginaResult<ContratoResumoResponse>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static IResult Criado(Result<ContratoResponse> result) =>
        result.Match(c => Results.Created($"/api/v1/contratos/{c.IdContrato}", c));
}

/// <summary>Contratação pelo próprio cliente. Campos opcionais para a validação devolver as mensagens do card.</summary>
public sealed record ContratarRequest(
    Guid? IdPlano,
    TipoCobertura? TipoCobertura,
    IReadOnlyList<Guid>? Dependentes,
    Periodicidade? Periodicidade,
    int? DiaVencimento);

/// <summary>Contratação pela equipe da funerária, que informa o titular.</summary>
public sealed record ContratarParaClienteRequest(
    Guid? IdCliente,
    Guid? IdPlano,
    TipoCobertura? TipoCobertura,
    IReadOnlyList<Guid>? Dependentes,
    Periodicidade? Periodicidade,
    int? DiaVencimento);
