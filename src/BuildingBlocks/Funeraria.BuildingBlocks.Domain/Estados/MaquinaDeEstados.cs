using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.BuildingBlocks.Domain.Estados;

/// <summary>
/// Tabela de transições de status (ARQ-01): cada entidade declara num lugar só quais ações levam de qual estado a qual.
/// Implementação própria, sem a biblioteca Stateless: uma tabela basta para translado, certidão e funeral.
/// </summary>
public sealed class MaquinaDeEstados<TEstado, TAcao>
    where TEstado : struct, Enum
    where TAcao : struct, Enum
{
    private readonly Dictionary<(TEstado De, TAcao Acao), TEstado> _transicoes = [];

    /// <summary>A <paramref name="acao"/> leva de qualquer um dos estados <paramref name="de"/> para <paramref name="para"/>.</summary>
    public MaquinaDeEstados<TEstado, TAcao> Permitir(TAcao acao, TEstado para, params TEstado[] de)
    {
        foreach (TEstado origem in de)
        {
            if (!_transicoes.TryAdd((origem, acao), para))
            {
                throw new InvalidOperationException($"Transição {origem} --{acao}--> já declarada.");
            }
        }

        return this;
    }

    /// <summary>Próximo estado, ou erro de validação se a ação não é permitida no estado atual.</summary>
    public Result<TEstado> Disparar(TEstado atual, TAcao acao) =>
        _transicoes.TryGetValue((atual, acao), out TEstado proximo)
            ? proximo
            : Result.Failure<TEstado>(Error.Validacao(
                "Estado.TransicaoInvalida",
                $"Não é possível {Nome(acao)} quando o status é {Nome(atual)}."));

    public IReadOnlyList<TAcao> AcoesPermitidas(TEstado atual) =>
        [.. _transicoes.Keys.Where(k => EqualityComparer<TEstado>.Default.Equals(k.De, atual)).Select(k => k.Acao)];

    /// <summary>Todas as transições declaradas, para os testes percorrerem a tabela.</summary>
    public IReadOnlyCollection<(TEstado De, TAcao Acao, TEstado Para)> Transicoes =>
        [.. _transicoes.Select(t => (t.Key.De, t.Key.Acao, t.Value))];

    /// <summary>Nome no formato da API: EmTransito → EM_TRANSITO.</summary>
    public static string Nome<T>(T valor)
        where T : struct, Enum => NomeApi.De(valor);
}

/// <summary>Registro de uma transição: quem, quando, de onde para onde e por quê (ARQ-01).</summary>
public sealed class HistoricoStatus<TEstado> : Entity<Guid>
    where TEstado : struct, Enum
{
    private HistoricoStatus(Guid id)
        : base(id)
    {
    }

    private HistoricoStatus()
    {
    }

    public TEstado De { get; private set; }

    public TEstado Para { get; private set; }

    public string Acao { get; private set; } = null!;

    public Guid? IdUsuario { get; private set; }

    public string? Observacao { get; private set; }

    public DateTime Em { get; private set; }

    internal static HistoricoStatus<TEstado> Criar(TEstado de, TEstado para, string acao, Guid? idUsuario, string? observacao, DateTime em) =>
        new(Guid.CreateVersion7())
        {
            De = de,
            Para = para,
            Acao = acao,
            IdUsuario = idUsuario,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            Em = em
        };
}

/// <summary>
/// Agregado com status controlado por uma <see cref="MaquinaDeEstados{TEstado, TAcao}"/>. Cada transição grava o histórico
/// e chama <see cref="AoMudarStatus"/>, onde a entidade publica o próprio evento de domínio (SignalR, notificações).
/// A concorrência otimista (xmin) do DbContext impede duas transições simultâneas a partir do mesmo estado.
/// </summary>
public abstract class AgregadoComStatus<TEstado, TAcao> : AggregateRoot<Guid>
    where TEstado : struct, Enum
    where TAcao : struct, Enum
{
    private readonly List<HistoricoStatus<TEstado>> _historico = [];

    protected AgregadoComStatus(Guid id, TEstado inicial)
        : base(id)
    {
        Status = inicial;
    }

    protected AgregadoComStatus()
    {
    }

    public TEstado Status { get; private set; }

    public IReadOnlyList<HistoricoStatus<TEstado>> Historico => _historico;

    protected abstract MaquinaDeEstados<TEstado, TAcao> Maquina { get; }

    public IReadOnlyList<TAcao> AcoesPermitidas => Maquina.AcoesPermitidas(Status);

    public Result Executar(TAcao acao, DateTime agora, Guid? idUsuario, string? observacao = null)
    {
        Result<TEstado> proximo = Maquina.Disparar(Status, acao);
        if (proximo.IsFailure)
        {
            return proximo.Error;
        }

        TEstado anterior = Status;
        Status = proximo.Value;
        _historico.Add(HistoricoStatus<TEstado>.Criar(anterior, Status, MaquinaDeEstados<TEstado, TAcao>.Nome(acao), idUsuario, observacao, agora));
        AoMudarStatus(anterior, Status, acao);
        return Result.Success();
    }

    /// <summary>Registra o estado inicial no histórico (chamar ao criar a entidade).</summary>
    protected void RegistrarInicio(DateTime agora, Guid? idUsuario) =>
        _historico.Add(HistoricoStatus<TEstado>.Criar(Status, Status, "CRIAR", idUsuario, null, agora));

    protected abstract void AoMudarStatus(TEstado anterior, TEstado atual, TAcao acao);
}
