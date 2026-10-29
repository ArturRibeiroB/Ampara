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

    /// <summary>Quem da funerária cuida do atendimento e em qual função (FUN-08).</summary>
    public IReadOnlyList<MembroEquipeFuneral> Equipe => _equipe;

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

    public static readonly Error MembroNaoEncontrado =
        Error.NaoEncontrado("Funeral.MembroNaoEncontrado", "Esta pessoa não está na equipe do funeral.");

    public static readonly Error MembroInvalido =
        Error.Validacao("Funeral.MembroInvalido", "A pessoa precisa ser gestor ou funcionário ativo desta funerária.");

    public static readonly Error Encerrado =
        Error.Conflito("Funeral.Encerrado", "Funeral concluído ou cancelado não pode ser alterado.");

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
