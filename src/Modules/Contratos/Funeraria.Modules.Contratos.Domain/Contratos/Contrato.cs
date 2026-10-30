using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Results;
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

    /// <summary>Por que o contrato foi cancelado (CON-03).</summary>
    public string? MotivoCancelamento { get; private set; }

    /// <summary>Conta do consultor que fez a contratação com a família, para acompanhamento e comissão (DEC-11).</summary>
    public Guid? IdConsultor { get; private set; }

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
        DateOnly hoje,
        Guid? idConsultor = null)
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
            CarenciaDias = plano.CarenciaDias,
            IdConsultor = idConsultor
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

    /// <summary>Primeira cobrança paga ou pendência quitada (COB-01, COB-03): a cobertura passa a valer.</summary>
    public void Ativar()
    {
        if (Status is StatusContrato.APagar or StatusContrato.Suspenso)
        {
            Status = StatusContrato.Ativo;
        }
    }

    /// <summary>Atraso além da tolerância (COB-03, DEC-12): a cobertura fica suspensa até a quitação.</summary>
    public void Suspender()
    {
        if (Status == StatusContrato.Ativo)
        {
            Status = StatusContrato.Suspenso;
        }
    }

    /// <summary>
    /// Pessoas cobertas enquanto o contrato valia: as atuais ou, no contrato cancelado, as que saíram com o cancelamento.
    /// O histórico do contrato continua visível depois do cancelamento (CON-03).
    /// </summary>
    public IEnumerable<PessoaCoberta> PessoasNaVigencia() =>
        Status == StatusContrato.Cancelado
            ? _pessoasCobertas.Where(p => p.DataRemocao == DataFim)
            : _pessoasCobertas.Where(p => p.DataRemocao is null);

    /// <summary>
    /// Inclui um dependente depois da contratação (CON-02). A carência dele conta a partir de hoje e o novo valor vale a
    /// partir da próxima cobrança. Dependente, titular e limite do plano já foram conferidos pelo caso de uso.
    /// </summary>
    public Result<PessoaCoberta> IncluirDependente(Guid idDependente, DateOnly hoje)
    {
        if (Status == StatusContrato.Cancelado)
        {
            return ContratoErros.ContratoCancelado;
        }

        if (_pessoasCobertas.Any(p => p.IdDependente == idDependente && p.DataRemocao is null))
        {
            return ContratoErros.DependenteEmContratoVigente;
        }

        var pessoa = PessoaCoberta.Dependente(idDependente, hoje);
        _pessoasCobertas.Add(pessoa);
        if (TipoCobertura == TipoCobertura.Titular)
        {
            TipoCobertura = TipoCobertura.TitularEDependentes;
        }

        RecalcularValor();
        return pessoa;
    }

    /// <summary>
    /// Tira o dependente do contrato (CON-02). A linha fica, com a data de saída, para o histórico. Contrato só de
    /// dependentes precisa manter ao menos um.
    /// </summary>
    public Result RemoverDependente(Guid idDependente, DateOnly hoje)
    {
        if (Status == StatusContrato.Cancelado)
        {
            return ContratoErros.ContratoCancelado;
        }

        PessoaCoberta? pessoa = _pessoasCobertas.FirstOrDefault(p => p.IdDependente == idDependente && p.DataRemocao is null);
        if (pessoa is null)
        {
            return ContratoErros.DependenteNaoCoberto;
        }

        int restantes = QuantidadeDependentes - 1;
        if (TipoCobertura == TipoCobertura.SomenteDependentes && restantes == 0)
        {
            return ContratoErros.UltimoDependente;
        }

        pessoa.Remover(hoje);
        if (TipoCobertura == TipoCobertura.TitularEDependentes && restantes == 0)
        {
            TipoCobertura = TipoCobertura.Titular;
        }

        RecalcularValor();
        return Result.Success();
    }

    /// <summary>
    /// Cancela o contrato (CON-03). As pessoas saem com a data do cancelamento, o que libera os dependentes para outro
    /// contrato; o Financeiro cancela as cobranças futuras e mantém as vencidas em aberto.
    /// </summary>
    public Result Cancelar(string motivo, DateOnly hoje)
    {
        if (Status == StatusContrato.Cancelado)
        {
            return ContratoErros.ContratoCancelado;
        }

        Status = StatusContrato.Cancelado;
        DataFim = hoje;
        MotivoCancelamento = motivo.Trim();
        foreach (PessoaCoberta pessoa in _pessoasCobertas.Where(p => p.DataRemocao is null))
        {
            pessoa.Remover(hoje);
        }

        Raise(new ContratoCanceladoDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>Reajuste anual (COB-08): novos valores, vigentes a partir da fatura seguinte. O histórico fica no Financeiro.</summary>
    public void Reajustar(decimal valorPlano, decimal valorPorDependente)
    {
        ValorPlano = valorPlano;
        ValorPorDependente = valorPorDependente;
        ValorTotal = ValorPlano + (ValorPorDependente * QuantidadeDependentes);
    }

    public int QuantidadeDependentes =>
        _pessoasCobertas.Count(p => p.Tipo == TipoPessoaCoberta.Dependente && p.DataRemocao is null);

    private void RecalcularValor()
    {
        ValorTotal = ValorPlano + (ValorPorDependente * QuantidadeDependentes);
        Raise(new PessoasDoContratoAlteradasDomainEvent(Id));
    }

    /// <summary>
    /// Se a pessoa pode usar o plano hoje (CON-04): contrato ativo e adimplente e carência cumprida, contada da data de
    /// inclusão da pessoa com a carência gravada na contratação.
    /// </summary>
    public Elegibilidade AvaliarElegibilidade(PessoaCoberta pessoa, DateOnly hoje)
    {
        DateOnly carenciaCumpridaEm = pessoa.DataInclusao.AddDays(CarenciaDias);

        string? motivo = Status switch
        {
            StatusContrato.Cancelado => "Contrato cancelado.",
            StatusContrato.Suspenso => "Contrato suspenso por falta de pagamento.",
            StatusContrato.APagar => "Contrato aguardando o pagamento da primeira cobrança.",
            _ when pessoa.DataRemocao is not null => "Pessoa removida do contrato.",
            _ when hoje < carenciaCumpridaEm => $"Em carência até {carenciaCumpridaEm:dd/MM/yyyy}.",
            _ => null
        };

        return new Elegibilidade(motivo is null, motivo, carenciaCumpridaEm);
    }
}

/// <summary>Resposta do CON-04 para uma pessoa coberta.</summary>
/// <param name="Elegivel">Pode usar o plano hoje.</param>
/// <param name="Motivo">Por que não pode; vazio quando pode.</param>
/// <param name="CarenciaCumpridaEm">Data em que a carência termina (ou terminou).</param>
public sealed record Elegibilidade(bool Elegivel, string? Motivo, DateOnly CarenciaCumpridaEm);

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

    internal void Remover(DateOnly hoje) => DataRemocao = hoje;
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

/// <summary>Dependente incluído ou removido (CON-02): muda o valor e a quantidade de dependentes da assinatura.</summary>
public sealed record PessoasDoContratoAlteradasDomainEvent(Guid IdContrato) : DomainEvent;

public sealed record ContratoCanceladoDomainEvent(Guid IdContrato) : DomainEvent;
