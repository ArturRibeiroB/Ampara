using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Estados;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Funeral.Domain.Pedidos;

/// <summary>
/// Pedido a um fornecedor parceiro com os itens do funeral que ele entrega (FUN-09). O fornecedor vê só o necessário
/// (nome do falecido, local, horário e itens) pela atribuição do TER-02, onde aceita ou recusa.
/// </summary>
public sealed class Pedido : AgregadoComStatus<StatusPedido, AcaoPedido>, ITenantEntity, IAuditableEntity
{
    private Pedido(Guid id, Guid tenantId)
        : base(id, StatusPedido.Pendente)
    {
        TenantId = tenantId;
    }

    private Pedido()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdFuneral { get; private set; }

    public Guid IdTerceiro { get; private set; }

    /// <summary>Atribuição no Terceiros: é por ela que o fornecedor aceita ou recusa (TER-02).</summary>
    public Guid? IdAtribuicao { get; private set; }

    public string NomeFalecido { get; private set; } = null!;

    public IReadOnlyList<ItemDoPedido> Itens { get; private set; } = [];

    public string LocalEntrega { get; private set; } = null!;

    /// <summary>Quando entregar (UTC).</summary>
    public DateTime DataHoraEntrega { get; private set; }

    public string? Observacoes { get; private set; }

    public string? MotivoRecusa { get; private set; }

    protected override MaquinaDeEstados<StatusPedido, AcaoPedido> Maquina => EstadosPedido.Maquina;

    /// <summary>Ainda vale: os itens dele não entram em outro pedido.</summary>
    public bool Ativo => Status is not (StatusPedido.Recusado or StatusPedido.Cancelado);

    /// <summary>O fornecedor ainda vai entregar: o cancelamento do funeral cancela.</summary>
    public bool Aberto => Status is StatusPedido.Pendente or StatusPedido.Aceito or StatusPedido.EmPreparo;

    public static Pedido Criar(
        Guid tenantId,
        Guid idFuneral,
        string nomeFalecido,
        Guid idTerceiro,
        IReadOnlyList<ItemDoPedido> itens,
        string localEntrega,
        DateTime dataHoraEntrega,
        string? observacoes,
        DateTime agora,
        Guid? idUsuario)
    {
        var pedido = new Pedido(Guid.CreateVersion7(), tenantId)
        {
            IdFuneral = idFuneral,
            NomeFalecido = nomeFalecido,
            IdTerceiro = idTerceiro,
            Itens = [.. itens],
            LocalEntrega = localEntrega.Trim(),
            DataHoraEntrega = dataHoraEntrega,
            Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim()
        };

        pedido.RegistrarInicio(agora, idUsuario);
        return pedido;
    }

    public void VincularAtribuicao(Guid idAtribuicao) => IdAtribuicao = idAtribuicao;

    public Result Recusar(string motivo, DateTime agora)
    {
        Result recusado = Executar(AcaoPedido.Recusar, agora, idUsuario: null, motivo);
        if (recusado.IsSuccess)
        {
            MotivoRecusa = motivo;
        }

        return recusado;
    }

    /// <summary>Atualização feita pelo fornecedor no portal: EM_PREPARO ou ENTREGUE (FUN-09).</summary>
    public Result AtualizarPeloFornecedor(StatusPedido destino, DateTime agora, Guid? idUsuario, string? observacao) =>
        EstadosPedido.AcaoDoFornecedor(destino) is AcaoPedido acao
            ? Executar(acao, agora, idUsuario, observacao)
            : Error.Validacao("Estado.TransicaoInvalida", "O fornecedor só muda o pedido para EM_PREPARO ou ENTREGUE.");

    protected override void AoMudarStatus(StatusPedido anterior, StatusPedido atual, AcaoPedido acao) =>
        Raise(new PedidoStatusAlteradoDomainEvent(Id, anterior, atual));
}

/// <summary>Item do funeral no pedido, como o fornecedor vê.</summary>
public sealed record ItemDoPedido(Guid IdFuneralItem, string Nome, string? Variacao, int Quantidade);

/// <summary>PENDENTE → ACEITO → EM_PREPARO → ENTREGUE; recusa só no PENDENTE; cancelamento enquanto não foi entregue.</summary>
public enum StatusPedido
{
    Pendente = 1,
    Aceito = 2,
    EmPreparo = 3,
    Entregue = 4,
    Recusado = 5,
    Cancelado = 6
}

public enum AcaoPedido
{
    Aceitar = 1,
    Recusar = 2,
    IniciarPreparo = 3,
    Entregar = 4,
    Cancelar = 5
}

/// <summary>Transições do pedido (ARQ-01, FUN-09).</summary>
public static class EstadosPedido
{
    // O cancelamento do pendente não está no desenho do card, mas é preciso quando o funeral é cancelado antes do aceite.
    public static readonly MaquinaDeEstados<StatusPedido, AcaoPedido> Maquina = new MaquinaDeEstados<StatusPedido, AcaoPedido>()
        .Permitir(AcaoPedido.Aceitar, StatusPedido.Aceito, StatusPedido.Pendente)
        .Permitir(AcaoPedido.Recusar, StatusPedido.Recusado, StatusPedido.Pendente)
        .Permitir(AcaoPedido.IniciarPreparo, StatusPedido.EmPreparo, StatusPedido.Aceito)
        .Permitir(AcaoPedido.Entregar, StatusPedido.Entregue, StatusPedido.Aceito, StatusPedido.EmPreparo)
        .Permitir(AcaoPedido.Cancelar, StatusPedido.Cancelado, StatusPedido.Pendente, StatusPedido.Aceito, StatusPedido.EmPreparo);

    public static AcaoPedido? AcaoDoFornecedor(StatusPedido destino) => destino switch
    {
        StatusPedido.EmPreparo => AcaoPedido.IniciarPreparo,
        StatusPedido.Entregue => AcaoPedido.Entregar,
        _ => null
    };
}

public sealed record PedidoStatusAlteradoDomainEvent(Guid IdPedido, StatusPedido Anterior, StatusPedido Atual) : DomainEvent;

public static class PedidoErros
{
    public static readonly Error NaoEncontrado = Error.NaoEncontrado("Pedido.NaoEncontrado", "Pedido não encontrado.");

    public static readonly Error NadaAPedir = Error.Conflito(
        "Pedido.NadaAPedir", "Nenhum item do funeral com fornecedor está esperando pedido.");

    public static readonly Error SoPedidoRecusado = Error.Conflito(
        "Pedido.SoPedidoRecusado", "Só um pedido recusado pode ser enviado a outro fornecedor.");

    public static readonly Error FornecedorInvalido = Error.Validacao(
        "Pedido.FornecedorInvalido", "O fornecedor precisa ser um terceiro do tipo FORNECEDOR, ativo nesta funerária.");
}

public interface IPedidoRepository
{
    Task<Pedido?> ObterAsync(Guid idPedido, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Pedido>> ListarDoFuneralAsync(Guid idFuneral, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Pedido>> ListarDoTerceiroAsync(Guid idTerceiro, StatusPedido? status, CancellationToken cancellationToken = default);

    void Adicionar(Pedido pedido);
}
