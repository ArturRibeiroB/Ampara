using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Funeral.Application.Locais;
using Funeraria.Modules.Funeral.Domain.Locais;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Funeral.Endpoints;

/// <summary>
/// Locais, agenda e reservas (FUN-05). O cadastro de locais é configuração da funerária (FUNERARIA); a agenda e as
/// reservas são da equipe interna (FUNERARIA e FUNCIONARIO).
/// </summary>
internal sealed class LocaisEndpoints : IEndpoint
{
    private const string Tag = "Funeral";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("gestao/locais", async (ISender sender, CancellationToken ct, bool ativos = false) =>
                (await sender.Send(new ListarLocaisQuery(ativos), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("ListarLocais")
            .WithSummary("Locais da funerária (salas, cemitérios, crematórios, igrejas)")
            .Produces<IReadOnlyList<LocalResponse>>();

        app.MapPost("gestao/locais", async (LocalRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CriarLocalCommand(r.Nome ?? string.Empty, r.Tipo, r.Endereco, r.Proprio, r.IntervaloPreparacaoMinutos), ct))
                .Match(l => Results.Created($"/api/v1/gestao/locais/{l.IdLocal}", l)))
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag)
            .WithName("CriarLocal")
            .WithSummary("Cadastra um local")
            .WithDescription("Tipos: SALA_VELORIO, CEMITERIO, CREMATORIO, IGREJA, OUTRO. Locais próprios (proprio = true) têm agenda "
                + "controlada, sem conflito de horário, com intervalo de preparação entre reservas (padrão 60 minutos).")
            .Produces<LocalResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        app.MapPut("gestao/locais/{id:guid}", async (Guid id, LocalRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AtualizarLocalCommand(id, r.Nome ?? string.Empty, r.Tipo, r.Endereco, r.Proprio, r.IntervaloPreparacaoMinutos), ct))
                .Match(Results.Ok))
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag)
            .WithName("AtualizarLocal")
            .WithSummary("Altera um local (o novo intervalo vale para as próximas reservas)")
            .Produces<LocalResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPatch("gestao/locais/{id:guid}/status", async (Guid id, StatusLocalRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusLocalCommand(id, r.Ativo), ct)).Match(Results.NoContent))
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag)
            .WithName("AlterarStatusLocal")
            .WithSummary("Ativa ou desativa um local (não é excluído)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("gestao/locais/{id:guid}/agenda", async (Guid id, DateOnly data, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AgendaLocalQuery(id, data), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("AgendaLocal")
            .WithSummary("Reservas do local no dia (UTC), com o funeral de cada uma")
            .Produces<AgendaResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("funerais/{id:guid}/reservas", async (Guid id, ReservaRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ReservarLocalCommand(id, r.IdLocal, r.Inicio, r.Fim), ct))
                .Match(reserva => Results.Created($"/api/v1/funerais/{id}", reserva)))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("ReservarLocal")
            .WithSummary("Reserva um local para o funeral")
            .WithDescription("Num local próprio, horário ocupado (considerando o intervalo de preparação) responde 409.")
            .Produces<ReservaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapDelete("funerais/{id:guid}/reservas/{idReserva:guid}", async (Guid id, Guid idReserva, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CancelarReservaCommand(id, idReserva), ct)).Match(Results.NoContent))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("CancelarReserva")
            .WithSummary("Cancela a reserva e libera o horário")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record LocalRequest(string? Nome, TipoLocal? Tipo, string? Endereco, bool Proprio, int? IntervaloPreparacaoMinutos);

public sealed record StatusLocalRequest(bool? Ativo);

public sealed record ReservaRequest(Guid? IdLocal, DateTime? Inicio, DateTime? Fim);
