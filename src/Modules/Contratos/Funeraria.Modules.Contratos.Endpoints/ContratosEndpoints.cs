using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Contratos.Application;
using Funeraria.Modules.Contratos.Application.Commands.CriarContrato;
using Funeraria.Modules.Contratos.Domain.Contratos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Contratos.Endpoints;

/// <summary>
/// Contratação (card Criar Contrato). O cliente contrata para si, com o titular vindo do token; a equipe da funerária
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
