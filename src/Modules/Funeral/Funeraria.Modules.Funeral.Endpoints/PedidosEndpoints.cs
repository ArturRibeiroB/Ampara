using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Funeral.Application.Pedidos;
using Funeraria.Modules.Funeral.Domain.Pedidos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Funeral.Endpoints;

/// <summary>
/// Pedidos a fornecedores (FUN-09). A equipe gera e reenvia; o fornecedor acompanha e avança no portal do terceiro.
/// Aceite e recusa são pela atribuição (POST /terceiros/atribuicoes/{id}/aceite e /recusa, TER-02).
/// </summary>
internal sealed class PedidosEndpoints : IEndpoint
{
    private const string Tag = "Funeral";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("funerais/{id:guid}/pedidos", async (Guid id, GerarPedidosRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GerarPedidosCommand(id, r.LocalEntrega, r.DataHoraEntrega, r.Observacoes), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("GerarPedidos")
            .WithSummary("Gera um pedido por fornecedor com os itens do funeral ligados a ele")
            .WithDescription("Só entram os itens com fornecedor que ainda não estão num pedido ativo. Itens de fornecedor desativado ficam em "
                + "itens_sem_fornecedor_ativo. Cada pedido aparece no portal do fornecedor para aceite.")
            .Produces<GeracaoPedidosResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("funerais/{id:guid}/pedidos", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PedidosDoFuneralQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("PedidosDoFuneral")
            .WithSummary("Pedidos do funeral, com o status de cada fornecedor")
            .Produces<IReadOnlyList<PedidoResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("funerais/{id:guid}/pedidos/{idPedido:guid}/reenviar", async (Guid id, Guid idPedido, ReenviarPedidoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ReenviarPedidoCommand(id, idPedido, r.IdTerceiro), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("ReenviarPedido")
            .WithSummary("Manda os itens de um pedido recusado para outro fornecedor")
            .Produces<PedidoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("terceiros/pedidos", async (ISender sender, CancellationToken ct, string? status = null) =>
                (await sender.Send(new MeusPedidosQuery(status), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.TerceiroFornecedor)
            .WithTags("Portal do terceiro")
            .WithName("MeusPedidos")
            .WithSummary("Pedidos do fornecedor logado: o que entregar, onde e quando")
            .Produces<IReadOnlyList<PedidoResponse>>()
            .ProducesValidationProblem();

        app.MapPatch("terceiros/pedidos/{id:guid}/status", async (Guid id, AtualizarPedidoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AtualizarPedidoCommand(id, r.Status, r.Observacao), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.TerceiroFornecedor)
            .WithTags("Portal do terceiro")
            .WithName("AtualizarPedido")
            .WithSummary("O fornecedor avança o pedido: EM_PREPARO ou ENTREGUE")
            .Produces<PedidoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record GerarPedidosRequest(string? LocalEntrega, DateTime? DataHoraEntrega, string? Observacoes);

public sealed record ReenviarPedidoRequest(Guid? IdTerceiro);

public sealed record AtualizarPedidoRequest(StatusPedido? Status, string? Observacao);
