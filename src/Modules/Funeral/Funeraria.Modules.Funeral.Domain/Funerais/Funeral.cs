using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Estados;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;
using Funeraria.Modules.Funeral.Domain.Beneficiarios;
using Funeraria.Modules.Funeral.Domain.Preferencias;

namespace Funeraria.Modules.Funeral.Domain.Funerais;

/// <summary>
/// Atendimento funeral de um beneficiário (card Registrar Funeral). Começa REGISTRADO e segue a máquina do FUN-06, com
/// histórico de cada mudança. Local, itens, pedidos e translado ficam nos cards FUN-03, FUN-05, FUN-09 e TRA-01 (AJU-01).
/// </summary>
public sealed class Funeral : AgregadoComStatus<StatusFuneral, AcaoFuneral>, ITenantEntity, IAuditableEntity
{
    private readonly List<MembroEquipeFuneral> _equipe = [];
    private readonly List<FuneralItem> _itens = [];
    private readonly List<Orcamento> _orcamentos = [];

    private Funeral(Guid id, Guid tenantId)
        : base(id, StatusFuneral.Registrado)
    {
        TenantId = tenantId;
    }

    private Funeral()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdBeneficiario { get; private set; }

    public Guid IdContrato { get; private set; }

    /// <summary>Titular do contrato, que acompanha o funeral (FUN-07).</summary>
    public Guid IdCliente { get; private set; }

    /// <summary>Copiado do beneficiário para as listagens e para o que os terceiros veem (TER-02).</summary>
    public string NomeFalecido { get; private set; } = null!;

    /// <summary>Data e hora previstas da cerimônia (UTC).</summary>
    public DateTime DataHoraPrevista { get; private set; }

    public Destinacao Destinacao { get; private set; }

    public string? Observacoes { get; private set; }

    /// <summary>Itens e serviços escolhidos pela família (FUN-03), com o preço do momento da escolha.</summary>
    public IReadOnlyList<FuneralItem> Itens => _itens;

    /// <summary>Quem da funerária cuida do atendimento e em qual função (FUN-08).</summary>
    public IReadOnlyList<MembroEquipeFuneral> Equipe => _equipe;

    /// <summary>Orçamentos dos itens adicionais (FUN-04). Cada um cobre só o que os aprovados antes dele não cobriram.</summary>
    public IReadOnlyList<Orcamento> Orcamentos => _orcamentos;

    protected override MaquinaDeEstados<StatusFuneral, AcaoFuneral> Maquina => EstadosFuneral.Maquina;

    /// <summary>Encerrado: concluído ou cancelado. Itens, salas e pedidos não mudam mais (FUN-03).</summary>
    public bool Encerrado => Status is StatusFuneral.Concluido or StatusFuneral.Cancelado;

    /// <summary>A data do funeral não pode ser anterior ao falecimento (que já não pode ser anterior ao nascimento).</summary>
    public static Result<Funeral> Registrar(
        Beneficiario beneficiario,
        DateTime dataHoraPrevista,
        Destinacao destinacao,
        string? observacoes,
        DateTime agora,
        Guid? idUsuario)
    {
        if (DateOnly.FromDateTime(dataHoraPrevista) < beneficiario.DataFalecimento)
        {
            return FuneralErros.AntesDoFalecimento;
        }

        var funeral = new Funeral(Guid.CreateVersion7(), beneficiario.TenantId)
        {
            IdBeneficiario = beneficiario.Id,
            IdContrato = beneficiario.IdContrato,
            IdCliente = beneficiario.IdCliente,
            NomeFalecido = beneficiario.Nome,
            DataHoraPrevista = dataHoraPrevista,
            Destinacao = destinacao,
            Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim()
        };

        funeral.RegistrarInicio(agora, idUsuario);
        funeral.Raise(new FuneralRegistradoDomainEvent(funeral.Id));
        return funeral;
    }

    /// <summary>Muda para o status pedido, se a transição for permitida (FUN-06).</summary>
    public Result MudarPara(StatusFuneral destino, DateTime agora, Guid? idUsuario, string? observacao) =>
        EstadosFuneral.AcaoPara(destino) is AcaoFuneral acao
            ? Executar(acao, agora, idUsuario, observacao)
            : FuneralErros.TransicaoInvalida(Status, destino);

    /// <summary>Inclui um item escolhido pela família (FUN-03). Item, variação e estoque já foram conferidos no catálogo.</summary>
    public Result<FuneralItem> AdicionarItem(ItemEscolhido escolhido, DateTime agora)
    {
        if (Encerrado)
        {
            return FuneralErros.Encerrado;
        }

        var item = FuneralItem.Criar(escolhido, agora);
        _itens.Add(item);
        SubstituirOrcamentoPendente();
        return item;
    }

    /// <returns>O item removido, para devolver o estoque baixado.</returns>
    public Result<FuneralItem> RemoverItem(Guid idFuneralItem)
    {
        if (Encerrado)
        {
            return FuneralErros.Encerrado;
        }

        FuneralItem? item = _itens.FirstOrDefault(i => i.Id == idFuneralItem);
        if (item is null)
        {
            return FuneralErros.ItemNaoEncontrado;
        }

        if (_orcamentos.Any(o => o.Status == StatusOrcamento.Aprovado && o.Itens.Any(l => l.IdFuneralItem == idFuneralItem)))
        {
            return FuneralErros.ItemJaAprovado;
        }

        _itens.Remove(item);
        SubstituirOrcamentoPendente();
        return item;
    }

    /// <summary>Orçamento com os adicionais que ainda não foram aprovados (FUN-04). Substitui o pendente, que ficou velho.</summary>
    /// <param name="adicionais">Os adicionais de agora, com a cobertura do plano já aplicada.</param>
    /// <param name="agora">Data da geração.</param>
    /// <param name="idUsuario">Quem gerou.</param>
    public Result<Orcamento> GerarOrcamento(IReadOnlyList<LinhaOrcamento> adicionais, DateTime agora, Guid? idUsuario)
    {
        if (Encerrado)
        {
            return FuneralErros.Encerrado;
        }

        var jaAprovados = _orcamentos.Where(o => o.Status == StatusOrcamento.Aprovado).SelectMany(o => o.Itens)
            .GroupBy(l => (l.IdFuneralItem, l.SomenteDiferencaVariacao))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantidade));

        List<LinhaOrcamento> linhas = [];
        foreach (LinhaOrcamento linha in adicionais)
        {
            int falta = linha.Quantidade - jaAprovados.GetValueOrDefault((linha.IdFuneralItem, linha.SomenteDiferencaVariacao));
            if (falta > 0)
            {
                linhas.Add(linha with { Quantidade = falta, Subtotal = linha.PrecoUnitario * falta });
            }
        }

        if (linhas.Sum(l => l.Subtotal) <= 0)
        {
            return FuneralErros.SemAdicionais;
        }

        SubstituirOrcamentoPendente();
        var orcamento = Orcamento.Criar(linhas, agora, idUsuario);
        _orcamentos.Add(orcamento);
        return orcamento;
    }

    /// <summary>Registra o aceite da família (FUN-04). Aprovar de novo não muda nada: serve para tentar a fatura outra vez.</summary>
    public Result<Orcamento> AprovarOrcamento(Guid idOrcamento, string nomeResponsavel, DateTime agora, Guid? idUsuario)
    {
        Orcamento? orcamento = _orcamentos.FirstOrDefault(o => o.Id == idOrcamento);
        if (orcamento is null)
        {
            return FuneralErros.OrcamentoNaoEncontrado;
        }

        if (orcamento.Status == StatusOrcamento.Aprovado)
        {
            return orcamento;
        }

        if (orcamento.Status != StatusOrcamento.Pendente)
        {
            return FuneralErros.OrcamentoDesatualizado;
        }

        if (Encerrado)
        {
            return FuneralErros.Encerrado;
        }

        orcamento.Aprovar(nomeResponsavel, agora, idUsuario);
        return orcamento;
    }

    /// <summary>Funeral cancelado: o pendente perde o efeito.</summary>
    /// <returns>As faturas dos aprovados, para cancelar as que ainda estão em aberto.</returns>
    public IReadOnlyList<Guid> CancelarOrcamentos()
    {
        foreach (Orcamento pendente in _orcamentos.Where(o => o.Status == StatusOrcamento.Pendente))
        {
            pendente.MudarPara(StatusOrcamento.Cancelado);
        }

        return [.. _orcamentos.Where(o => o.IdFatura is not null).Select(o => o.IdFatura!.Value)];
    }

    private void SubstituirOrcamentoPendente()
    {
        foreach (Orcamento pendente in _orcamentos.Where(o => o.Status == StatusOrcamento.Pendente))
        {
            pendente.MudarPara(StatusOrcamento.Substituido);
        }
    }

    /// <summary>Funeral cancelado: os itens ficam no histórico, mas o estoque baixado volta ao catálogo (FUN-03).</summary>
    /// <returns>Os itens cujo estoque precisa ser devolvido.</returns>
    public IReadOnlyList<FuneralItem> LiberarEstoque()
    {
        FuneralItem[] baixados = [.. _itens.Where(i => i.EstoqueBaixado)];
        foreach (FuneralItem item in baixados)
        {
            item.EstoqueDevolvido();
        }

        return baixados;
    }

    /// <summary>Coloca a pessoa na equipe do funeral, ou troca a função dela (FUN-08). A pessoa já foi conferida na Identidade.</summary>
    public Result AtribuirMembro(Guid idUsuario, string nome, FuncaoEquipe funcao, DateTime agora)
    {
        if (Encerrado)
        {
            return FuneralErros.Encerrado;
        }

        MembroEquipeFuneral? atual = _equipe.FirstOrDefault(m => m.IdUsuario == idUsuario);
        if (atual is null)
        {
            _equipe.Add(MembroEquipeFuneral.Criar(idUsuario, nome, funcao, agora));
        }
        else
        {
            atual.MudarFuncao(funcao);
        }

        return Result.Success();
    }

    public Result RemoverMembro(Guid idUsuario)
    {
        if (Encerrado)
        {
            return FuneralErros.Encerrado;
        }

        MembroEquipeFuneral? membro = _equipe.FirstOrDefault(m => m.IdUsuario == idUsuario);
        if (membro is null)
        {
            return FuneralErros.MembroNaoEncontrado;
        }

        _equipe.Remove(membro);
        return Result.Success();
    }

    protected override void AoMudarStatus(StatusFuneral anterior, StatusFuneral atual, AcaoFuneral acao) =>
        Raise(new FuneralStatusAlteradoDomainEvent(Id, anterior, atual));
}

/// <summary>
/// Item do catálogo no funeral (FUN-03). Nome, variação e preços são copiados na escolha: mudar o catálogo depois não muda
/// o funeral nem o orçamento (FUN-01, FUN-04).
/// </summary>
public sealed class FuneralItem : Entity<Guid>
{
    private FuneralItem(Guid id)
        : base(id)
    {
    }

    private FuneralItem()
    {
    }

    public Guid IdItemCatalogo { get; private set; }

    public Guid? IdVariacao { get; private set; }

    public string Nome { get; private set; } = null!;

    public string Categoria { get; private set; } = null!;

    public string? NomeVariacao { get; private set; }

    public int Quantidade { get; private set; }

    /// <summary>Preço do item no catálogo, sem a variação.</summary>
    public decimal PrecoBase { get; private set; }

    /// <summary>Diferença da variação escolhida (pode ser negativa). Paga à parte mesmo quando o item está coberto.</summary>
    public decimal AcrescimoVariacao { get; private set; }

    /// <summary>Fornecedor que recebe o pedido deste item (FUN-09).</summary>
    public Guid? IdTerceiroFornecedor { get; private set; }

    /// <summary>A quantidade saiu do estoque do catálogo e volta se o item sair ou o funeral for cancelado.</summary>
    public bool EstoqueBaixado { get; private set; }

    public OrigemItem Origem { get; private set; }

    public DateTime IncluidoEm { get; private set; }

    public decimal PrecoUnitario => PrecoBase + AcrescimoVariacao;

    internal static FuneralItem Criar(ItemEscolhido e, DateTime agora) =>
        new(Guid.CreateVersion7())
        {
            IdItemCatalogo = e.IdItemCatalogo,
            IdVariacao = e.IdVariacao,
            Nome = e.Nome,
            Categoria = e.Categoria,
            NomeVariacao = e.NomeVariacao,
            Quantidade = e.Quantidade,
            PrecoBase = e.PrecoBase,
            AcrescimoVariacao = e.AcrescimoVariacao,
            IdTerceiroFornecedor = e.IdTerceiroFornecedor,
            EstoqueBaixado = e.EstoqueBaixado,
            Origem = e.Origem,
            IncluidoEm = agora
        };

    internal void EstoqueDevolvido() => EstoqueBaixado = false;
}

/// <summary>
/// Orçamento dos itens adicionais (FUN-04). As linhas são uma cópia: mudar os itens depois não muda o que a família viu, e
/// qualquer mudança nos itens tira o efeito do pendente.
/// </summary>
public sealed class Orcamento : Entity<Guid>
{
    private Orcamento(Guid id)
        : base(id)
    {
    }

    private Orcamento()
    {
    }

    public StatusOrcamento Status { get; private set; }

    public IReadOnlyList<LinhaOrcamento> Itens { get; private set; } = [];

    public decimal Total { get; private set; }

    public DateTime GeradoEm { get; private set; }

    public Guid? IdUsuarioGeracao { get; private set; }

    /// <summary>Responsável pela família que aprovou.</summary>
    public string? AprovadoPor { get; private set; }

    public DateTime? AprovadoEm { get; private set; }

    /// <summary>Conta que registrou a aprovação: a da equipe ou a do próprio titular.</summary>
    public Guid? IdUsuarioAprovacao { get; private set; }

    public Guid? IdFatura { get; private set; }

    public string? LinkPagamento { get; private set; }

    internal static Orcamento Criar(IReadOnlyList<LinhaOrcamento> linhas, DateTime agora, Guid? idUsuario) =>
        new(Guid.CreateVersion7())
        {
            Status = StatusOrcamento.Pendente,
            Itens = linhas,
            Total = linhas.Sum(l => l.Subtotal),
            GeradoEm = agora,
            IdUsuarioGeracao = idUsuario
        };

    internal void Aprovar(string nomeResponsavel, DateTime agora, Guid? idUsuario)
    {
        Status = StatusOrcamento.Aprovado;
        AprovadoPor = nomeResponsavel.Trim();
        AprovadoEm = agora;
        IdUsuarioAprovacao = idUsuario;
    }

    internal void MudarPara(StatusOrcamento status) => Status = status;

    public void RegistrarFatura(Guid idFatura, string? linkPagamento)
    {
        IdFatura = idFatura;
        LinkPagamento = linkPagamento;
    }
}

public sealed record LinhaOrcamento(
    Guid IdFuneralItem, string Nome, string? Variacao, int Quantidade, decimal PrecoUnitario, decimal Subtotal, bool SomenteDiferencaVariacao);

public enum StatusOrcamento
{
    Pendente = 1,
    Aprovado = 2,

    /// <summary>Os itens mudaram, ou saiu um orçamento mais novo.</summary>
    Substituido = 3,
    Cancelado = 4
}

public sealed record ItemEscolhido(
    Guid IdItemCatalogo,
    Guid? IdVariacao,
    string Nome,
    string Categoria,
    string? NomeVariacao,
    int Quantidade,
    decimal PrecoBase,
    decimal AcrescimoVariacao,
    Guid? IdTerceiroFornecedor,
    bool EstoqueBaixado,
    OrigemItem Origem);

public enum OrigemItem
{
    /// <summary>Escolhido pela equipe com a família.</summary>
    Manual = 1,

    /// <summary>Veio das preferências registradas em vida (FUN-02).</summary>
    Preferencia = 2
}

/// <summary>Pessoa da equipe interna no atendimento (FUN-08). O nome é copiado da conta para as telas.</summary>
public sealed class MembroEquipeFuneral : Entity<Guid>
{
    private MembroEquipeFuneral(Guid id)
        : base(id)
    {
    }

    private MembroEquipeFuneral()
    {
    }

    public Guid IdUsuario { get; private set; }

    public string Nome { get; private set; } = null!;

    public FuncaoEquipe Funcao { get; private set; }

    public DateTime AtribuidoEm { get; private set; }

    internal static MembroEquipeFuneral Criar(Guid idUsuario, string nome, FuncaoEquipe funcao, DateTime agora) =>
        new(Guid.CreateVersion7()) { IdUsuario = idUsuario, Nome = nome, Funcao = funcao, AtribuidoEm = agora };

    internal void MudarFuncao(FuncaoEquipe funcao) => Funcao = funcao;
}

public enum FuncaoEquipe
{
    AgenteFunerario = 1,
    Cerimonialista = 2,
    Motorista = 3,
    Outra = 4
}

public sealed record FuneralRegistradoDomainEvent(Guid IdFuneral) : DomainEvent;

public sealed record FuneralStatusAlteradoDomainEvent(Guid IdFuneral, StatusFuneral Anterior, StatusFuneral Atual) : DomainEvent;

public static class FuneralErros
{
    public static readonly Error NaoEncontrado =
        Error.NaoEncontrado("Funeral.NaoEncontrado", "Funeral não encontrado.");

    public static readonly Error JaRegistrado =
        Error.Conflito("Funeral.JaRegistrado", "Este beneficiário já tem um funeral registrado.");

    public static readonly Error AntesDoFalecimento =
        Error.Validacao("Funeral.AntesDoFalecimento", "A data do funeral não pode ser anterior à data de falecimento.");

    public static readonly Error ItemNaoEncontrado =
        Error.NaoEncontrado("Funeral.ItemNaoEncontrado", "Item não encontrado neste funeral.");

    public static readonly Error MembroNaoEncontrado =
        Error.NaoEncontrado("Funeral.MembroNaoEncontrado", "Esta pessoa não está na equipe do funeral.");

    public static readonly Error MembroInvalido =
        Error.Validacao("Funeral.MembroInvalido", "A pessoa precisa ser gestor ou funcionário ativo desta funerária.");

    public static readonly Error Encerrado =
        Error.Conflito("Funeral.Encerrado", "Funeral concluído ou cancelado não pode ser alterado.");

    public static readonly Error ItemJaAprovado =
        Error.Conflito("Funeral.ItemJaAprovado", "Este item está em um orçamento aprovado e não pode ser retirado.");

    public static readonly Error SemAdicionais =
        Error.Conflito("Funeral.SemAdicionais", "Não há itens adicionais a orçar.");

    public static readonly Error OrcamentoNaoEncontrado =
        Error.NaoEncontrado("Funeral.OrcamentoNaoEncontrado", "Orçamento não encontrado neste funeral.");

    public static readonly Error OrcamentoDesatualizado =
        Error.Conflito("Funeral.OrcamentoDesatualizado", "Os itens mudaram depois deste orçamento. Gere e apresente um novo.");

    public static Error TransicaoInvalida(StatusFuneral atual, StatusFuneral destino) =>
        Error.Validacao("Estado.TransicaoInvalida",
            $"Não é possível mudar de {NomeApi.De(atual)} para {NomeApi.De(destino)}.");
}

public interface IFuneralRepository
{
    Task<Funeral?> ObterAsync(Guid idFuneral, CancellationToken cancellationToken = default);

    /// <summary>Funeral não cancelado do beneficiário.</summary>
    Task<bool> ExisteAtivoDoBeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken = default);

    /// <summary>Funerais em andamento em que a pessoa está na equipe, pela data da cerimônia (FUN-08).</summary>
    Task<IReadOnlyList<Funeral>> ListarAtribuidosAsync(Guid idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Funeral do contrato ainda não concluído nem cancelado (CON-03).</summary>
    Task<bool> ExisteEmAndamentoNoContratoAsync(Guid idContrato, CancellationToken cancellationToken = default);

    void Adicionar(Funeral funeral);
}
