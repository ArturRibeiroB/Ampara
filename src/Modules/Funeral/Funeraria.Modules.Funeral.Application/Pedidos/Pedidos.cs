using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Funeral.Application.Abstractions;
using Funeraria.Modules.Funeral.Contracts.IntegrationEvents;
using Funeraria.Modules.Funeral.Domain.Funerais;
using Funeraria.Modules.Funeral.Domain.Pedidos;
using Funeraria.Modules.Terceiros.Contracts;
using Funeraria.Modules.Terceiros.Contracts.IntegrationEvents;
using FuneralEntity = Funeraria.Modules.Funeral.Domain.Funerais.Funeral;

namespace Funeraria.Modules.Funeral.Application.Pedidos;

/// <summary>
/// Gera um pedido por fornecedor com os itens do funeral ligados a ele que ainda não estão num pedido ativo (FUN-09).
/// Cada pedido vira uma atribuição no portal do terceiro (TER-02).
/// </summary>
public sealed record GerarPedidosCommand(Guid IdFuneral, string? LocalEntrega, DateTime? DataHoraEntrega, string? Observacoes)
    : ICommand<GeracaoPedidosResponse>;

/// <summary>Pedido recusado volta para a funerária, que manda os mesmos itens para outro fornecedor (FUN-09).</summary>
public sealed record ReenviarPedidoCommand(Guid IdFuneral, Guid IdPedido, Guid? IdTerceiro) : ICommand<PedidoResponse>;

public sealed record PedidosDoFuneralQuery(Guid IdFuneral) : IQuery<IReadOnlyList<PedidoResponse>>;

/// <summary>Pedidos do fornecedor logado (FUN-09). Status: PENDENTE, ACEITO, EM_PREPARO, ENTREGUE, RECUSADO ou CANCELADO.</summary>
public sealed record MeusPedidosQuery(string? Status) : IQuery<IReadOnlyList<PedidoResponse>>;

/// <summary>O fornecedor avança o pedido: EM_PREPARO ou ENTREGUE. Aceite e recusa são pela atribuição (TER-02).</summary>
public sealed record AtualizarPedidoCommand(Guid IdPedido, StatusPedido? Status, string? Observacao) : ICommand<PedidoResponse>;

public sealed record PedidoResponse(
    Guid IdPedido,
    Guid IdFuneral,
    Guid IdTerceiro,
    string NomeFalecido,
    StatusPedido Status,
    IReadOnlyList<ItemDoPedido> Itens,
    string LocalEntrega,
    DateTime DataHoraEntrega,
    string? Observacoes,
    string? MotivoRecusa)
{
    public static PedidoResponse De(Pedido p) => new(
        p.Id, p.IdFuneral, p.IdTerceiro, p.NomeFalecido, p.Status, p.Itens, p.LocalEntrega, p.DataHoraEntrega, p.Observacoes, p.MotivoRecusa);
}

/// <param name="Pedidos">Pedidos gerados, um por fornecedor.</param>
/// <param name="ItensSemFornecedorAtivo">Itens que ficaram de fora porque o fornecedor deles está inativo ou não é FORNECEDOR.</param>
public sealed record GeracaoPedidosResponse(IReadOnlyList<PedidoResponse> Pedidos, IReadOnlyList<Guid> ItensSemFornecedorAtivo);

internal sealed class GerarPedidosCommandValidator : AbstractValidator<GerarPedidosCommand>
{
    public GerarPedidosCommandValidator()
    {
        RuleFor(c => c.LocalEntrega)
            .NotEmpty().WithMessage("Local de entrega é obrigatório.")
            .MaximumLength(300).WithMessage("Local de entrega deve ter no máximo 300 caracteres.");
        RuleFor(c => c.DataHoraEntrega).NotNull().WithMessage("Data e hora da entrega são obrigatórias.");
        RuleFor(c => c.Observacoes).MaximumLength(1000).WithMessage("Observações devem ter no máximo 1000 caracteres.");
    }
}

internal sealed class ReenviarPedidoCommandValidator : AbstractValidator<ReenviarPedidoCommand>
{
    public ReenviarPedidoCommandValidator() => RuleFor(c => c.IdTerceiro).NotEmpty().WithMessage("Informe o novo fornecedor.");
}

internal sealed class AtualizarPedidoCommandValidator : AbstractValidator<AtualizarPedidoCommand>
{
    public AtualizarPedidoCommandValidator()
    {
        RuleFor(c => c.Status).NotNull().WithMessage("Informe o novo status (EM_PREPARO ou ENTREGUE).");
        RuleFor(c => c.Observacao).MaximumLength(1000).WithMessage("Observação deve ter no máximo 1000 caracteres.");
    }
}

/// <summary>Grava o pedido e cria a atribuição no portal do fornecedor.</summary>
public sealed class EnvioDePedidos(IPedidoRepository pedidos, IFuneralUnitOfWork unitOfWork, ITerceirosAtribuicoes atribuicoes)
{
    public async Task<Result> EnviarAsync(Pedido pedido, CancellationToken cancellationToken)
    {
        pedidos.Adicionar(pedido);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        Result<Guid> atribuicao = await atribuicoes.AtribuirAsync(new NovaAtribuicao(
            pedido.IdTerceiro,
            pedido.Id,
            new ResumoPedido(pedido.NomeFalecido, pedido.LocalEntrega, pedido.DataHoraEntrega,
                [.. pedido.Itens.Select(i => new ItemPedido(i.Variacao is null ? i.Nome : $"{i.Nome} ({i.Variacao})", i.Quantidade, null))])),
            cancellationToken);
        if (atribuicao.IsFailure)
        {
            return atribuicao.Error;
        }

        pedido.VincularAtribuicao(atribuicao.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public static async Task<bool> FornecedorAtivoAsync(ITerceirosConsultas terceiros, Guid idTerceiro, CancellationToken cancellationToken) =>
        await terceiros.ObterAsync(idTerceiro, cancellationToken) is { Tipo: "FORNECEDOR", Ativo: true };
}

public sealed class GerarPedidosCommandHandler(
    IFuneralRepository funerais,
    IPedidoRepository pedidos,
    ITerceirosConsultas terceiros,
    EnvioDePedidos envio,
    ICurrentUser usuario,
    TimeProvider timeProvider)
    : ICommandHandler<GerarPedidosCommand, GeracaoPedidosResponse>
{
    public async ValueTask<Result<GeracaoPedidosResponse>> Handle(GerarPedidosCommand command, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(command.IdFuneral, cancellationToken);
        if (funeral is null)
        {
            return FuneralErros.NaoEncontrado;
        }

        if (funeral.Encerrado)
        {
            return FuneralErros.Encerrado;
        }

        HashSet<Guid> jaPedidos = [.. (await pedidos.ListarDoFuneralAsync(funeral.Id, cancellationToken))
            .Where(p => p.Ativo).SelectMany(p => p.Itens).Select(i => i.IdFuneralItem)];
        IGrouping<Guid, FuneralItem>[] porFornecedor = [.. funeral.Itens
            .Where(i => i.IdTerceiroFornecedor is not null && !jaPedidos.Contains(i.Id))
            .GroupBy(i => i.IdTerceiroFornecedor!.Value)];
        if (porFornecedor.Length == 0)
        {
            return PedidoErros.NadaAPedir;
        }

        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        DateTime entrega = DateTime.SpecifyKind(command.DataHoraEntrega!.Value.ToUniversalTime(), DateTimeKind.Utc);
        var gerados = new List<PedidoResponse>();
        var semFornecedor = new List<Guid>();

        foreach (IGrouping<Guid, FuneralItem> grupo in porFornecedor)
        {
            // Fornecedor desativado depois de ligado ao item: os itens ficam de fora para a funerária escolher outro.
            if (!await EnvioDePedidos.FornecedorAtivoAsync(terceiros, grupo.Key, cancellationToken))
            {
                semFornecedor.AddRange(grupo.Select(i => i.Id));
                continue;
            }

            var pedido = Pedido.Criar(
                funeral.TenantId, funeral.Id, funeral.NomeFalecido, grupo.Key,
                [.. grupo.Select(i => new ItemDoPedido(i.Id, i.Nome, i.NomeVariacao, i.Quantidade))],
                command.LocalEntrega!, entrega, command.Observacoes, agora, usuario.IdUsuario);
            Result enviado = await envio.EnviarAsync(pedido, cancellationToken);
            if (enviado.IsFailure)
            {
                return Result.Failure<GeracaoPedidosResponse>(enviado.Error);
            }

            gerados.Add(PedidoResponse.De(pedido));
        }

        return new GeracaoPedidosResponse(gerados, semFornecedor);
    }
}

public sealed class ReenviarPedidoCommandHandler(
    IFuneralRepository funerais,
    IPedidoRepository pedidos,
    ITerceirosConsultas terceiros,
    EnvioDePedidos envio,
    ICurrentUser usuario,
    TimeProvider timeProvider)
    : ICommandHandler<ReenviarPedidoCommand, PedidoResponse>
{
    public async ValueTask<Result<PedidoResponse>> Handle(ReenviarPedidoCommand command, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(command.IdFuneral, cancellationToken);
        Pedido? recusado = await pedidos.ObterAsync(command.IdPedido, cancellationToken);
        if (funeral is null || recusado is null || recusado.IdFuneral != funeral.Id)
        {
            return PedidoErros.NaoEncontrado;
        }

        if (funeral.Encerrado)
        {
            return FuneralErros.Encerrado;
        }

        if (recusado.Status != StatusPedido.Recusado)
        {
            return PedidoErros.SoPedidoRecusado;
        }

        if (!await EnvioDePedidos.FornecedorAtivoAsync(terceiros, command.IdTerceiro!.Value, cancellationToken))
        {
            return PedidoErros.FornecedorInvalido;
        }

        var pedido = Pedido.Criar(
            funeral.TenantId, funeral.Id, funeral.NomeFalecido, command.IdTerceiro.Value, recusado.Itens,
            recusado.LocalEntrega, recusado.DataHoraEntrega, recusado.Observacoes, timeProvider.GetUtcNow().UtcDateTime, usuario.IdUsuario);
        Result enviado = await envio.EnviarAsync(pedido, cancellationToken);
        return enviado.IsSuccess ? PedidoResponse.De(pedido) : Result.Failure<PedidoResponse>(enviado.Error);
    }
}

public sealed class PedidosDoFuneralQueryHandler(IFuneralRepository funerais, IPedidoRepository pedidos)
    : IQueryHandler<PedidosDoFuneralQuery, IReadOnlyList<PedidoResponse>>
{
    public async ValueTask<Result<IReadOnlyList<PedidoResponse>>> Handle(PedidosDoFuneralQuery query, CancellationToken cancellationToken) =>
        await funerais.ObterAsync(query.IdFuneral, cancellationToken) is null
            ? FuneralErros.NaoEncontrado
            : (await pedidos.ListarDoFuneralAsync(query.IdFuneral, cancellationToken)).Select(PedidoResponse.De).ToList();
}

public sealed class MeusPedidosQueryHandler(IPedidoRepository pedidos, ICurrentUser usuario)
    : IQueryHandler<MeusPedidosQuery, IReadOnlyList<PedidoResponse>>
{
    public async ValueTask<Result<IReadOnlyList<PedidoResponse>>> Handle(MeusPedidosQuery query, CancellationToken cancellationToken)
    {
        StatusPedido? status = null;
        if (query.Status is not null)
        {
            if (!Enum.TryParse(query.Status.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out StatusPedido lido)
                || !Enum.IsDefined(lido))
            {
                return Error.Validacao("Pedido.StatusInvalido", "Status inválido. Use PENDENTE, ACEITO, EM_PREPARO, ENTREGUE, RECUSADO ou CANCELADO.");
            }

            status = lido;
        }

        return (await pedidos.ListarDoTerceiroAsync(usuario.IdTerceiro!.Value, status, cancellationToken)).Select(PedidoResponse.De).ToList();
    }
}

public sealed class AtualizarPedidoCommandHandler(
    IPedidoRepository pedidos,
    IFuneralUnitOfWork unitOfWork,
    ITerceirosAtribuicoes atribuicoes,
    ICurrentUser usuario,
    TimeProvider timeProvider)
    : ICommandHandler<AtualizarPedidoCommand, PedidoResponse>
{
    public async ValueTask<Result<PedidoResponse>> Handle(AtualizarPedidoCommand command, CancellationToken cancellationToken)
    {
        // Pedido de outro fornecedor responde 404, como se não existisse (TER-02).
        Pedido? pedido = await pedidos.ObterAsync(command.IdPedido, cancellationToken);
        if (pedido is null || pedido.IdTerceiro != usuario.IdTerceiro)
        {
            return PedidoErros.NaoEncontrado;
        }

        Result atualizado = pedido.AtualizarPeloFornecedor(command.Status!.Value, timeProvider.GetUtcNow().UtcDateTime, usuario.IdUsuario, command.Observacao);
        if (atualizado.IsFailure)
        {
            return Result.Failure<PedidoResponse>(atualizado.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Entregue: sai da lista de pendências do fornecedor (TER-02).
        if (pedido.Status == StatusPedido.Entregue && pedido.IdAtribuicao is Guid idAtribuicao)
        {
            await atribuicoes.EncerrarAsync(idAtribuicao, concluida: true, cancellationToken);
        }

        return PedidoResponse.De(pedido);
    }
}

/// <summary>O fornecedor aceitou o pedido no portal (TER-02).</summary>
internal sealed class PedidoAceitoPeloFornecedor(IPedidoRepository pedidos, IFuneralUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IIntegrationEventHandler<AtribuicaoAceitaIntegrationEvent>
{
    public async Task Handle(AtribuicaoAceitaIntegrationEvent evento, CancellationToken cancellationToken)
    {
        if (evento.Tipo == "PEDIDO" && await pedidos.ObterAsync(evento.IdOrigem, cancellationToken) is Pedido pedido
            && pedido.Executar(AcaoPedido.Aceitar, timeProvider.GetUtcNow().UtcDateTime, idUsuario: null).IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>O fornecedor recusou o pedido no portal (TER-02): fica RECUSADO e a funerária reenvia a outro.</summary>
internal sealed class PedidoRecusadoPeloFornecedor(IPedidoRepository pedidos, IFuneralUnitOfWork unitOfWork, TimeProvider timeProvider)
    : IIntegrationEventHandler<AtribuicaoRecusadaIntegrationEvent>
{
    public async Task Handle(AtribuicaoRecusadaIntegrationEvent evento, CancellationToken cancellationToken)
    {
        if (evento.Tipo == "PEDIDO" && await pedidos.ObterAsync(evento.IdOrigem, cancellationToken) is Pedido pedido
            && pedido.Recusar(evento.Motivo, timeProvider.GetUtcNow().UtcDateTime).IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>Funeral cancelado cancela os pedidos abertos e tira da lista dos fornecedores (FUN-09).</summary>
internal sealed class CancelarPedidosDoFuneralCancelado(
    IPedidoRepository pedidos,
    IFuneralUnitOfWork unitOfWork,
    ITerceirosAtribuicoes atribuicoes,
    TimeProvider timeProvider)
    : IDomainEventHandler<FuneralStatusAlteradoDomainEvent>
{
    public async Task Handle(FuneralStatusAlteradoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.Atual != StatusFuneral.Cancelado)
        {
            return;
        }

        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        Pedido[] abertos = [.. (await pedidos.ListarDoFuneralAsync(domainEvent.IdFuneral, cancellationToken)).Where(p => p.Aberto)];
        foreach (Pedido pedido in abertos)
        {
            pedido.Executar(AcaoPedido.Cancelar, agora, idUsuario: null, "Funeral cancelado.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (Pedido pedido in abertos.Where(p => p.IdAtribuicao is not null))
        {
            await atribuicoes.EncerrarAsync(pedido.IdAtribuicao!.Value, concluida: false, cancellationToken);
        }
    }
}

/// <summary>Publica cada mudança do pedido para o Notificacoes (FUN-09).</summary>
internal sealed class PedidoStatusAlteradoDomainEventHandler(IPedidoRepository pedidos, ITerceirosConsultas terceiros, IEventBus eventBus)
    : IDomainEventHandler<PedidoStatusAlteradoDomainEvent>
{
    public async Task Handle(PedidoStatusAlteradoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await pedidos.ObterAsync(domainEvent.IdPedido, cancellationToken) is not Pedido pedido)
        {
            return;
        }

        TerceiroResumoDto? fornecedor = await terceiros.ObterAsync(pedido.IdTerceiro, cancellationToken);
        await eventBus.PublishAsync(
            new PedidoStatusAlteradoIntegrationEvent(
                pedido.TenantId, pedido.Id, pedido.IdFuneral, fornecedor?.NomeRazaoSocial ?? string.Empty, fornecedor?.IdUsuario,
                pedido.NomeFalecido, NomeApi.De(domainEvent.Anterior), NomeApi.De(domainEvent.Atual)),
            cancellationToken);
    }
}
