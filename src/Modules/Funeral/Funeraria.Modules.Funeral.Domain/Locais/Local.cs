using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Funeral.Domain.Locais;

/// <summary>
/// Lugar usado no funeral (FUN-05): sala de velório própria, cemitério, crematório, igreja. Só os locais próprios têm
/// agenda controlada: uma reserva por horário, com o intervalo de preparação entre uma e outra.
/// </summary>
public sealed class Local : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private Local(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private Local()
    {
    }

    public Guid TenantId { get; private set; }

    public string Nome { get; private set; } = null!;

    public TipoLocal Tipo { get; private set; }

    public string? Endereco { get; private set; }

    /// <summary>Local da funerária (ex.: sala de velório): a agenda é controlada e não aceita conflito.</summary>
    public bool Proprio { get; private set; }

    /// <summary>Tempo para limpar e preparar o local entre duas reservas (só nos próprios).</summary>
    public int IntervaloPreparacaoMinutos { get; private set; }

    public bool Ativo { get; private set; }

    public static Local Criar(Guid tenantId, DadosLocal dados)
    {
        var local = new Local(Guid.CreateVersion7(), tenantId) { Ativo = true };
        local.Atualizar(dados);
        return local;
    }

    public void Atualizar(DadosLocal dados)
    {
        Nome = dados.Nome.Trim();
        Tipo = dados.Tipo;
        Endereco = string.IsNullOrWhiteSpace(dados.Endereco) ? null : dados.Endereco.Trim();
        Proprio = dados.Proprio;
        IntervaloPreparacaoMinutos = dados.Proprio ? dados.IntervaloPreparacaoMinutos : 0;
    }

    public void AlterarStatus(bool ativo) => Ativo = ativo;

    /// <summary>
    /// Reserva o local para o funeral. Fora dos locais próprios a reserva só registra onde e quando; nos próprios, o
    /// conflito de agenda é conferido pelo caso de uso e garantido no banco (restrição de exclusão).
    /// </summary>
    public Result<Reserva> Reservar(Guid idFuneral, DateTime inicio, DateTime fim)
    {
        if (!Ativo)
        {
            return LocalErros.Inativo;
        }

        if (fim <= inicio)
        {
            return LocalErros.PeriodoInvalido;
        }

        return Reserva.Criar(TenantId, Id, idFuneral, inicio, fim, Proprio, IntervaloPreparacaoMinutos);
    }
}

/// <summary>Uso de um local por um funeral, num período (UTC).</summary>
public sealed class Reserva : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private Reserva(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private Reserva()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdLocal { get; private set; }

    public Guid IdFuneral { get; private set; }

    public DateTime Inicio { get; private set; }

    public DateTime Fim { get; private set; }

    /// <summary>Até quando o local fica ocupado: o fim mais o intervalo de preparação. É o que entra na checagem de conflito.</summary>
    public DateTime OcupadoAte { get; private set; }

    /// <summary>Copiado do local: só as reservas de locais próprios entram na restrição de conflito do banco.</summary>
    public bool ControlaAgenda { get; private set; }

    public bool Cancelada { get; private set; }

    internal static Reserva Criar(Guid tenantId, Guid idLocal, Guid idFuneral, DateTime inicio, DateTime fim, bool controlaAgenda, int intervaloMinutos) =>
        new(Guid.CreateVersion7(), tenantId)
        {
            IdLocal = idLocal,
            IdFuneral = idFuneral,
            Inicio = inicio,
            Fim = fim,
            OcupadoAte = fim.AddMinutes(intervaloMinutos),
            ControlaAgenda = controlaAgenda
        };

    /// <summary>Conflita com outro período do mesmo local, considerando o intervalo de preparação dos dois lados.</summary>
    public bool Conflita(Reserva outra) =>
        !Cancelada && !outra.Cancelada && IdLocal == outra.IdLocal && Inicio < outra.OcupadoAte && outra.Inicio < OcupadoAte;

    public void Cancelar() => Cancelada = true;
}

public sealed record DadosLocal(string Nome, TipoLocal Tipo, string? Endereco, bool Proprio, int IntervaloPreparacaoMinutos);

public enum TipoLocal
{
    SalaVelorio = 1,
    Cemiterio = 2,
    Crematorio = 3,
    Igreja = 4,
    Outro = 5
}

public static class LocalErros
{
    public static readonly Error NaoEncontrado = Error.NaoEncontrado("Local.NaoEncontrado", "Local não encontrado.");

    public static readonly Error Inativo = Error.Conflito("Local.Inativo", "Este local está desativado.");

    public static readonly Error PeriodoInvalido = Error.Validacao("Local.PeriodoInvalido", "O fim da reserva precisa ser depois do início.");

    public static readonly Error HorarioOcupado = Error.Conflito(
        "Local.HorarioOcupado", "O local já está reservado nesse horário (considerando o intervalo de preparação).");

    public static readonly Error ReservaNaoEncontrada = Error.NaoEncontrado("Local.ReservaNaoEncontrada", "Reserva não encontrada.");
}

public interface ILocalRepository
{
    Task<Local?> ObterAsync(Guid idLocal, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Local>> ListarAsync(bool somenteAtivos, CancellationToken cancellationToken = default);

    void Adicionar(Local local);
}

public interface IReservaRepository
{
    Task<Reserva?> ObterAsync(Guid idReserva, CancellationToken cancellationToken = default);

    /// <summary>Reservas ativas do local que encostam no período (para a checagem de conflito e a agenda).</summary>
    Task<IReadOnlyList<Reserva>> ListarDoLocalAsync(Guid idLocal, DateTime de, DateTime ate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Reserva>> ListarDoFuneralAsync(Guid idFuneral, CancellationToken cancellationToken = default);

    void Adicionar(Reserva reserva);
}
