using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Contratos.Domain.Contratos;

/// <summary>
/// Contrato de um cliente titular com um plano (card Criar Contrato). Guarda o valor e a periodicidade vigentes na
/// contratação: mudar o plano depois não muda contratos existentes (AJU-01).
/// </summary>
public sealed class Contrato : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private readonly List<PessoaCoberta> _pessoasCobertas = [];

    private Contrato(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private Contrato()
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>Cliente titular, responsável pelo contrato.</summary>
    public Guid IdCliente { get; private set; }

    public Guid IdPlano { get; private set; }

    /// <summary>Nome do plano na contratação, para o histórico não depender do plano atual.</summary>
    public string NomePlano { get; private set; } = null!;

    public TipoCobertura TipoCobertura { get; private set; }

    public Periodicidade Periodicidade { get; private set; }

    public int DiaVencimento { get; private set; }

    public DateOnly DataContratacao { get; private set; }

    /// <summary>Fim do contrato. Vazio enquanto ele estiver vigente; preenchido no cancelamento (CON-03).</summary>
    public DateOnly? DataFim { get; private set; }

    public StatusContrato Status { get; private set; }

    /// <summary>Valor do plano na periodicidade escolhida, na data da contratação.</summary>
    public decimal ValorPlano { get; private set; }

    /// <summary>Adicional por dependente na periodicidade escolhida, na data da contratação (DEC-01).</summary>
    public decimal ValorPorDependente { get; private set; }

    /// <summary>Valor de cada cobrança (mensal ou anual): plano + adicional dos dependentes cobertos.</summary>
    public decimal ValorTotal { get; private set; }

    /// <summary>Carência do plano na contratação, em dias (CON-04).</summary>
    public int CarenciaDias { get; private set; }

    public IReadOnlyList<PessoaCoberta> PessoasCobertas => _pessoasCobertas;

    /// <summary>
    /// Cria o contrato com as pessoas cobertas. Começa A_PAGAR: passa a ATIVO quando a primeira cobrança for paga
    /// (COB-01). Titular e dependentes já foram conferidos pelo caso de uso.
    /// </summary>
    public static Contrato Contratar(
        Guid tenantId,
        Guid idCliente,
        PlanoContratado plano,
        TipoCobertura tipoCobertura,
        Periodicidade periodicidade,
        int diaVencimento,
        IReadOnlyCollection<Guid> idsDependentes,
        DateOnly hoje)
    {
        if (tipoCobertura == TipoCobertura.Titular ? idsDependentes.Count > 0 : idsDependentes.Count == 0)
        {
            throw new ArgumentException("Dependentes não combinam com o tipo de cobertura.", nameof(idsDependentes));
        }

        var contrato = new Contrato(Guid.CreateVersion7(), tenantId)
        {
            IdCliente = idCliente,
            IdPlano = plano.IdPlano,
            NomePlano = plano.Nome,
            TipoCobertura = tipoCobertura,
            Periodicidade = periodicidade,
            DiaVencimento = diaVencimento,
            DataContratacao = hoje,
            Status = StatusContrato.APagar,
            ValorPlano = plano.Valor,
            ValorPorDependente = plano.ValorPorDependente,
            ValorTotal = plano.Valor + (plano.ValorPorDependente * idsDependentes.Count),
            CarenciaDias = plano.CarenciaDias
        };

        if (tipoCobertura != TipoCobertura.SomenteDependentes)
        {
            contrato._pessoasCobertas.Add(PessoaCoberta.Titular(hoje));
        }

        foreach (Guid idDependente in idsDependentes.Distinct())
        {
            contrato._pessoasCobertas.Add(PessoaCoberta.Dependente(idDependente, hoje));
        }

        contrato.Raise(new ContratoCriadoDomainEvent(contrato.Id));
        return contrato;
    }
}

/// <summary>Titular ou dependente coberto pelo contrato, com a data de inclusão que conta para a carência (CON-04).</summary>
public sealed class PessoaCoberta : Entity<Guid>
{
    private PessoaCoberta(Guid id)
        : base(id)
    {
    }

    private PessoaCoberta()
    {
    }

    public TipoPessoaCoberta Tipo { get; private set; }

    /// <summary>Preenchido quando a pessoa é um dependente.</summary>
    public Guid? IdDependente { get; private set; }

    public DateOnly DataInclusao { get; private set; }

    /// <summary>Saída do contrato (CON-02) ou cancelamento (CON-03).</summary>
    public DateOnly? DataRemocao { get; private set; }

    internal static PessoaCoberta Titular(DateOnly hoje) =>
        new(Guid.CreateVersion7()) { Tipo = TipoPessoaCoberta.Titular, DataInclusao = hoje };

    internal static PessoaCoberta Dependente(Guid idDependente, DateOnly hoje) =>
        new(Guid.CreateVersion7()) { Tipo = TipoPessoaCoberta.Dependente, IdDependente = idDependente, DataInclusao = hoje };
}

/// <summary>Dados do plano usados na contratação, já na periodicidade escolhida.</summary>
public sealed record PlanoContratado(Guid IdPlano, string Nome, decimal Valor, decimal ValorPorDependente, int CarenciaDias);

public enum TipoCobertura
{
    /// <summary>Somente o titular.</summary>
    Titular = 1,

    /// <summary>Titular e um ou mais dependentes.</summary>
    TitularEDependentes = 2,

    /// <summary>Só dependentes, sem o titular coberto. Depende do plano permitir.</summary>
    SomenteDependentes = 3
}

public enum TipoPessoaCoberta
{
    Titular = 1,
    Dependente = 2
}

/// <summary>Todo plano oferece as duas opções e o cliente escolhe na contratação (DEC-01).</summary>
public enum Periodicidade
{
    Mensal = 1,
    Anual = 2
}

/// <summary>Situação do contrato (COB-03). SUSPENSO substitui o "Vencido" do card original (AJU-01).</summary>
public enum StatusContrato
{
    Ativo = 1,
    APagar = 2,
    Suspenso = 3,
    Cancelado = 4
}

public sealed record ContratoCriadoDomainEvent(Guid IdContrato) : DomainEvent;
