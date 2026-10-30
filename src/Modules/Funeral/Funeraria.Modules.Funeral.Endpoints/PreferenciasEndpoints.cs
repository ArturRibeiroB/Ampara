using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Funeral.Application.Preferencias;
using Funeraria.Modules.Funeral.Domain.Preferencias;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Funeral.Endpoints;

/// <summary>Preferências de funeral registradas em vida pelo titular, para ele e para os dependentes (FUN-02).</summary>
internal sealed class PreferenciasEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder grupo = app.MapGroup("preferencias-funeral")
            .RequireAuthorization(Politicas.Cliente)
            .WithTags("Preferências de funeral")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapGet(string.Empty, async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListarPreferenciasQuery(), ct)).Match(Results.Ok))
            .WithName("ListarPreferenciasFuneral")
            .WithSummary("Titular e dependentes, com as preferências registradas e o que o plano de cada um cobre")
            .Produces<IReadOnlyList<PessoaPreferenciaResponse>>();

        grupo.MapPut("{idPessoa:guid}", async (Guid idPessoa, PreferenciaRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DefinirPreferenciaCommand(
                    idPessoa, r.Destinacao, r.TipoCerimonia, r.CrencaReligiosa, r.Musicas, r.Mensagens, r.Observacoes, r.Itens), ct)).Match(Results.Ok))
            .WithName("DefinirPreferenciaFuneral")
            .WithSummary("Registra ou altera as preferências do titular ou de um dependente dele")
            .WithDescription("id_pessoa é o id do cliente (titular) ou do dependente. A crença religiosa é dado sensível e fica cifrada.")
            .Produces<PessoaPreferenciaResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record PreferenciaRequest(
    Destinacao? Destinacao,
    TipoCerimonia? TipoCerimonia,
    string? CrencaReligiosa,
    IReadOnlyList<string>? Musicas,
    string? Mensagens,
    string? Observacoes,
    IReadOnlyList<ItemPreferidoRequest>? Itens);
