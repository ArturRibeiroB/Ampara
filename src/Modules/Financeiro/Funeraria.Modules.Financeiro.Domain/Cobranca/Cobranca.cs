using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Financeiro.Domain.Cobranca;

/// <summary>
/// Conta conectada da funerária no Stripe (DEC-09). O cadastro (CNPJ, conta bancária, sócios) é feito na página hospedada
/// pelo Stripe: aqui ficam só o id da conta e se ela já pode cobrar.
/// </summary>
public sealed class ContaPagamento : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private ContaPagamento(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private ContaPagamento()
    {
    }

    public Guid TenantId { get; private set; }

    public string IdStripeAccount { get; private set; } = null!;

    public bool CobrancasHabilitadas { get; private set; }

    public bool CadastroEnviado { get; private set; }

    public static ContaPagamento Criar(Guid tenantId, string idStripeAccount) =>
        new(Guid.CreateVersion7(), tenantId) { IdStripeAccount = idStripeAccount };

    /// <returns>A conta acabou de ficar pronta para cobrar.</returns>
    public bool AtualizarSituacao(bool cobrancasHabilitadas, bool cadastroEnviado)
    {
        bool ficouPronta = cobrancasHabilitadas && !CobrancasHabilitadas;
        CobrancasHabilitadas = cobrancasHabilitadas;
        CadastroEnviado = cadastroEnviado;
        return ficouPronta;
    }
}

/// <summary>Cliente da funerária no Stripe. O Customer fica na conta conectada, um por titular.</summary>
public sealed class ClienteStripe : Entity<Guid>, ITenantEntity
{
    private ClienteStripe(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private ClienteStripe()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdCliente { get; private set; }

    public string IdStripeCustomer { get; private set; } = null!;

    public static ClienteStripe Criar(Guid tenantId, Guid idCliente, string idStripeCustomer) =>
        new(Guid.CreateVersion7(), tenantId) { IdCliente = idCliente, IdStripeCustomer = idStripeCustomer };
}

/// <summary>
/// Cobrança recorrente de um contrato (COB-01). Quem gera as faturas é o Stripe; aqui ficam os ids, os valores vigentes do
/// contrato e a situação de pagamento. Nasce AGUARDANDO_CONTA quando a funerária ainda não concluiu o cadastro no Stripe.
/// </summary>
public sealed class Assinatura : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private readonly List<HistoricoValorAssinatura> _historico = [];

    private Assinatura(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private Assinatura()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdContrato { get; private set; }

    public Guid IdCliente { get; private set; }

    public Guid IdPlano { get; private set; }

    public string NomePlano { get; private set; } = null!;

    public PeriodicidadeCobranca Periodicidade { get; private set; }

    public int DiaVencimento { get; private set; }

    public DateOnly DataContratacao { get; private set; }

    public decimal ValorPlano { get; private set; }

    public decimal ValorPorDependente { get; private set; }

    public int QuantidadeDependentes { get; private set; }

    public StatusAssinatura Status { get; private set; }

    public string? IdStripeCustomer { get; private set; }

    public string? IdStripeSubscription { get; private set; }

    public string? IdItemPlano { get; private set; }

    public string? IdItemDependentes { get; private set; }

    /// <summary>Passou da tolerância com fatura em aberto: o contrato está suspenso (COB-03).</summary>
    public bool Inadimplente { get; private set; }

    /// <summary>Quando entrou em atraso, para o cancelamento depois de 90 dias (DEC-12).</summary>
    public DateOnly? InadimplenteDesde { get; private set; }

    /// <summary>Último reajuste; sem ele, o ciclo de reajuste conta da contratação (COB-08).</summary>
    public DateOnly? UltimoReajusteEm { get; private set; }

    /// <summary>Aviso de reajuste enviado para o próximo aniversário, para não repetir.</summary>
    public DateOnly? AvisoReajustePara { get; private set; }

    /// <summary>Fim do período já faturado, atualizado a cada fatura (renovação do anual, COB-02).</summary>
    public DateOnly? VigenteAte { get; private set; }

    public IReadOnlyList<HistoricoValorAssinatura> Historico => _historico;

    public decimal ValorTotal => ValorPlano + (ValorPorDependente * QuantidadeDependentes);

    public DateOnly ProximoReajuste => (UltimoReajusteEm ?? DataContratacao).AddYears(1);

    public static Assinatura Registrar(Guid tenantId, DadosAssinatura dados, DateOnly hoje)
    {
        var assinatura = new Assinatura(Guid.CreateVersion7(), tenantId)
        {
            IdContrato = dados.IdContrato,
            IdCliente = dados.IdCliente,
            IdPlano = dados.IdPlano,
            NomePlano = dados.NomePlano,
            Periodicidade = dados.Periodicidade,
            DiaVencimento = dados.DiaVencimento,
            DataContratacao = dados.DataContratacao,
            ValorPlano = dados.ValorPlano,
            ValorPorDependente = dados.ValorPorDependente,
            QuantidadeDependentes = dados.QuantidadeDependentes,
            Status = StatusAssinatura.AguardandoConta
        };

        assinatura._historico.Add(HistoricoValorAssinatura.Criar(hoje, dados.ValorPlano, dados.ValorPorDependente, "Contratação"));
        return assinatura;
    }

    public void Vincular(string idCustomer, string idSubscription, string idItemPlano, string idItemDependentes)
    {
        IdStripeCustomer = idCustomer;
        IdStripeSubscription = idSubscription;
        IdItemPlano = idItemPlano;
        IdItemDependentes = idItemDependentes;
        Status = StatusAssinatura.Ativa;
    }

    /// <summary>Vale a partir da próxima fatura (CON-02): a assinatura muda sem cobrança proporcional.</summary>
    public void AlterarDependentes(int quantidade) => QuantidadeDependentes = quantidade;

    public void Encerrar() => Status = StatusAssinatura.Cancelada;

    public void AtualizarVigencia(DateOnly? fimDoPeriodo)
    {
        if (fimDoPeriodo is DateOnly fim && (VigenteAte is null || fim > VigenteAte))
        {
            VigenteAte = fim;
        }
    }

    public void MarcarInadimplente(DateOnly hoje)
    {
        if (!Inadimplente)
        {
            Inadimplente = true;
            InadimplenteDesde = hoje;
            Raise(new AssinaturaInadimplenteDomainEvent(Id));
        }
    }

    public void MarcarEmDia()
    {
        Inadimplente = false;
        InadimplenteDesde = null;
        Raise(new AssinaturaEmDiaDomainEvent(Id));
    }

    public void RegistrarAvisoReajuste(DateOnly aniversario) => AvisoReajustePara = aniversario;

    /// <summary>Aplica o reajuste anual (COB-08): novo valor na fatura seguinte, com o histórico guardado.</summary>
    public void Reajustar(decimal valorPlano, decimal valorPorDependente, DateOnly aniversario)
    {
        ValorPlano = valorPlano;
        ValorPorDependente = valorPorDependente;
        UltimoReajusteEm = aniversario;
        _historico.Add(HistoricoValorAssinatura.Criar(aniversario, valorPlano, valorPorDependente, "Reajuste anual"));
        Raise(new AssinaturaReajustadaDomainEvent(Id));
    }
}

public sealed class HistoricoValorAssinatura : Entity<Guid>
{
    private HistoricoValorAssinatura(Guid id)
        : base(id)
    {
    }

    private HistoricoValorAssinatura()
    {
    }

    public DateOnly VigenteDesde { get; private set; }

    public decimal ValorPlano { get; private set; }

    public decimal ValorPorDependente { get; private set; }

    public string Motivo { get; private set; } = null!;

    internal static HistoricoValorAssinatura Criar(DateOnly desde, decimal valorPlano, decimal valorPorDependente, string motivo) =>
        new(Guid.CreateVersion7()) { VigenteDesde = desde, ValorPlano = valorPlano, ValorPorDependente = valorPorDependente, Motivo = motivo };
}

public sealed record DadosAssinatura(
    Guid IdContrato,
    Guid IdCliente,
    Guid IdPlano,
    string NomePlano,
    PeriodicidadeCobranca Periodicidade,
    int DiaVencimento,
    DateOnly DataContratacao,
    decimal ValorPlano,
    decimal ValorPorDependente,
    int QuantidadeDependentes);

/// <summary>
/// Cópia local de uma fatura do Stripe (COB-01), usada nas telas, na inadimplência e nos relatórios. O Stripe manda: a fatura
/// é criada e atualizada a partir dele (COB-02, COB-06), e o único valor próprio daqui é o de multa e juros (COB-09).
/// </summary>
public sealed class Fatura : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private Fatura(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private Fatura()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdContrato { get; private set; }

    public Guid IdCliente { get; private set; }

    public string IdStripeInvoice { get; private set; } = null!;

    public DateOnly Competencia { get; private set; }

    public decimal Valor { get; private set; }

    public decimal ValorMultaJuros { get; private set; }

    public DateOnly DataVencimento { get; private set; }

    public StatusFatura Status { get; private set; }

    public FormaPagamento? FormaPagamento { get; private set; }

    public string? LinkPagamento { get; private set; }

    public DateTime? DataPagamento { get; private set; }

    /// <summary>Quando o boleto ou Pix pago entrou em compensação: não conta como atraso (COB-03, COB-09).</summary>
    public DateOnly? ProcessandoDesde { get; private set; }

    /// <summary>Fatura fora do ciclo (primeira cobrança, multa e juros do anual, itens do funeral): não gera novo encargo.</summary>
    public bool Avulsa { get; private set; }

    /// <summary>Itens do funeral (FUN-04): atrasar não suspende o plano nem impede a reativação.</summary>
    public bool ForaDaInadimplencia { get; private set; }

    public string? ObservacaoBaixa { get; private set; }

    public bool EmAberto => Status is StatusFatura.Pendente or StatusFatura.Vencida or StatusFatura.Processando;

    public static Fatura Espelhar(Guid tenantId, Guid idContrato, Guid idCliente, FaturaEspelhada dados)
    {
        var fatura = new Fatura(Guid.CreateVersion7(), tenantId)
        {
            IdContrato = idContrato,
            IdCliente = idCliente,
            IdStripeInvoice = dados.IdStripeInvoice,
            Status = StatusFatura.Pendente,
            Avulsa = dados.Avulsa,
            ForaDaInadimplencia = dados.ForaDaInadimplencia
        };

        fatura.Atualizar(dados);
        return fatura;
    }

    /// <summary>Atualiza a partir do Stripe. Idempotente: o mesmo evento pode chegar mais de uma vez, fora de ordem.</summary>
    public void Atualizar(FaturaEspelhada dados)
    {
        Competencia = dados.Competencia;
        Valor = dados.Valor;
        DataVencimento = dados.DataVencimento;
        LinkPagamento = dados.LinkPagamento;

        switch (dados.Situacao)
        {
            case SituacaoFaturaStripe.Paga:
                RegistrarPagamento(dados.PagaEm ?? DateTime.UtcNow, dados.FormaPagamento ?? Cobranca.FormaPagamento.Cartao);
                break;
            case SituacaoFaturaStripe.Cancelada when Status != StatusFatura.Paga:
                Status = StatusFatura.Cancelada;
                break;
        }
    }

    /// <summary>Boleto ou Pix pago, aguardando a compensação.</summary>
    public void MarcarProcessando(DateOnly hoje)
    {
        if (Status is StatusFatura.Pendente or StatusFatura.Vencida)
        {
            Status = StatusFatura.Processando;
            ProcessandoDesde = hoje;
        }
    }

    /// <summary>A compensação falhou: volta a ficar em aberto, e o atraso volta a contar.</summary>
    public void PagamentoFalhou(DateOnly hoje)
    {
        if (Status == StatusFatura.Processando)
        {
            Status = hoje > DataVencimento ? StatusFatura.Vencida : StatusFatura.Pendente;
            ProcessandoDesde = null;
        }
    }

    public void MarcarVencida(DateOnly hoje)
    {
        if (Status == StatusFatura.Pendente && hoje > DataVencimento)
        {
            Status = StatusFatura.Vencida;
        }
    }

    public Result RegistrarBaixaManual(FormaPagamento forma, string observacao, DateTime agora)
    {
        if (Status == StatusFatura.Paga)
        {
            return FaturaErros.JaPaga;
        }

        if (Status == StatusFatura.Cancelada)
        {
            return FaturaErros.Cancelada;
        }

        ObservacaoBaixa = observacao.Trim();
        RegistrarPagamento(agora, forma);
        return Result.Success();
    }

    /// <summary>Multa e juros lançados quando a fatura em atraso é paga (COB-09).</summary>
    public void RegistrarEncargos(decimal valor) => ValorMultaJuros = valor;

    /// <summary>A fatura estava em compensação até o vencimento: o atraso não é do cliente (COB-09).</summary>
    public bool AtrasoCobravel(DateOnly dataPagamento) =>
        !Avulsa && dataPagamento > DataVencimento && (ProcessandoDesde is null || ProcessandoDesde > DataVencimento);

    private void RegistrarPagamento(DateTime em, FormaPagamento forma)
    {
        if (Status == StatusFatura.Paga)
        {
            return;
        }

        Status = StatusFatura.Paga;
        DataPagamento = em;
        FormaPagamento = forma;
        Raise(new FaturaPagaDomainEvent(Id));
    }
}

public sealed record FaturaEspelhada(
    string IdStripeInvoice,
    DateOnly Competencia,
    decimal Valor,
    DateOnly DataVencimento,
    SituacaoFaturaStripe Situacao,
    string? LinkPagamento,
    DateTime? PagaEm,
    FormaPagamento? FormaPagamento,
    bool Avulsa,
    bool ForaDaInadimplencia = false);

/// <summary>Evento recebido do Stripe (COB-06). O id do evento é único: o mesmo evento só é processado uma vez.</summary>
public sealed class EventoStripe : Entity<Guid>
{
    private EventoStripe(Guid id)
        : base(id)
    {
    }

    private EventoStripe()
    {
    }

    public string IdEvento { get; private set; } = null!;

    public string Tipo { get; private set; } = null!;

    public string? IdStripeAccount { get; private set; }

    public string Conteudo { get; private set; } = null!;

    public DateTime RecebidoEm { get; private set; }

    public DateTime? ProcessadoEm { get; private set; }

    public static EventoStripe Receber(string idEvento, string tipo, string? conta, string conteudo, DateTime agora) =>
        new(Guid.CreateVersion7()) { IdEvento = idEvento, Tipo = tipo, IdStripeAccount = conta, Conteudo = conteudo, RecebidoEm = agora };

    public void MarcarProcessado(DateTime agora) => ProcessadoEm = agora;
}

/// <summary>Lembrete de fatura já enviado (COB-04): um por fatura e dia relativo ao vencimento.</summary>
public sealed class LembreteFatura : Entity<Guid>, ITenantEntity
{
    private LembreteFatura(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private LembreteFatura()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdFatura { get; private set; }

    /// <summary>Negativo: dias antes do vencimento; zero: no dia; positivo: depois.</summary>
    public int DiasDoVencimento { get; private set; }

    public static LembreteFatura Criar(Guid tenantId, Guid idFatura, int diasDoVencimento) =>
        new(Guid.CreateVersion7(), tenantId) { IdFatura = idFatura, DiasDoVencimento = diasDoVencimento };
}

public enum PeriodicidadeCobranca
{
    Mensal = 1,
    Anual = 2
}

public enum StatusAssinatura
{
    AguardandoConta = 1,
    Ativa = 2,
    Cancelada = 3
}

public enum StatusFatura
{
    Pendente = 1,
    Processando = 2,
    Paga = 3,
    Vencida = 4,
    Cancelada = 5
}

public enum SituacaoFaturaStripe
{
    Aberta = 1,
    Paga = 2,
    Cancelada = 3
}

public enum FormaPagamento
{
    Cartao = 1,
    Pix = 2,
    Boleto = 3,
    Manual = 4
}

public sealed record AssinaturaInadimplenteDomainEvent(Guid IdAssinatura) : DomainEvent;

public sealed record AssinaturaEmDiaDomainEvent(Guid IdAssinatura) : DomainEvent;

public sealed record AssinaturaReajustadaDomainEvent(Guid IdAssinatura) : DomainEvent;

public sealed record FaturaPagaDomainEvent(Guid IdFatura) : DomainEvent;

public static class FaturaErros
{
    public static readonly Error NaoEncontrada = Error.NaoEncontrado("Fatura.NaoEncontrada", "Fatura não encontrada.");

    public static readonly Error ContratoNaoEncontrado = Error.NaoEncontrado("Fatura.ContratoNaoEncontrado", "Contrato não encontrado.");

    public static readonly Error JaPaga = Error.Conflito("Fatura.JaPaga", "Esta fatura já está paga.");

    public static readonly Error Cancelada = Error.Conflito("Fatura.Cancelada", "Esta fatura está cancelada.");

    public static readonly Error ContaNaoCadastrada =
        Error.NaoEncontrado("Pagamentos.ContaNaoCadastrada", "A funerária ainda não iniciou o cadastro de pagamentos.");
}

/// <summary>Regras de data da cobrança (COB-01, COB-03), sem depender de relógio nem de banco.</summary>
public static class CicloCobranca
{
    /// <summary>A fatura sai alguns dias antes do vencimento, para o cliente ter tempo de pagar (boleto, principalmente).</summary>
    public const int DiasDeAntecedencia = 5;

    /// <summary>
    /// Data em que o Stripe emite a próxima fatura. A primeira sai na contratação e cobre o ciclo até aqui; por isso o próximo
    /// vencimento fica a pelo menos 25 dias, para o cliente não pagar duas vezes em poucos dias.
    /// </summary>
    public static DateOnly PrimeiraAncora(DateOnly hoje, int diaVencimento, PeriodicidadeCobranca periodicidade)
    {
        if (periodicidade == PeriodicidadeCobranca.Anual)
        {
            DateOnly daquiUmAno = hoje.AddYears(1);
            return new DateOnly(daquiUmAno.Year, daquiUmAno.Month, diaVencimento).AddDays(-DiasDeAntecedencia);
        }

        var vencimento = new DateOnly(hoje.Year, hoje.Month, diaVencimento);
        while (vencimento.DayNumber - hoje.DayNumber < 25)
        {
            vencimento = vencimento.AddMonths(1);
        }

        return vencimento.AddDays(-DiasDeAntecedencia);
    }

    /// <summary>Último dia da tolerância: conta só dias úteis, sem fins de semana e feriados nacionais (DEC-12).</summary>
    public static DateOnly FimDaTolerancia(DateOnly vencimento, int diasUteis)
    {
        DateOnly dia = vencimento;
        for (int contados = 0; contados < diasUteis;)
        {
            dia = dia.AddDays(1);
            if (EhDiaUtil(dia))
            {
                contados++;
            }
        }

        return dia;
    }

    public static bool EhDiaUtil(DateOnly dia) =>
        dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !EhFeriadoNacional(dia);

    /// <summary>Multa fixa mais juros proporcionais aos dias de atraso, com meio para cima (COB-09).</summary>
    public static decimal Encargos(decimal valor, decimal multaPercentual, decimal jurosMensalPercentual, int diasDeAtraso)
    {
        decimal multa = valor * multaPercentual / 100m;
        decimal juros = valor * (jurosMensalPercentual / 100m / 30m) * diasDeAtraso;
        return decimal.Round(multa + juros, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Valor reajustado com duas casas, meio para cima (COB-08): o padrão do .NET arredonda para o par.</summary>
    public static decimal Reajustar(decimal valor, decimal percentual) =>
        decimal.Round(valor * (1 + (percentual / 100m)), 2, MidpointRounding.AwayFromZero);

    private static bool EhFeriadoNacional(DateOnly dia) =>
        (dia.Month, dia.Day) is (1, 1) or (4, 21) or (5, 1) or (9, 7) or (10, 12) or (11, 2) or (11, 15) or (11, 20) or (12, 25)
        || dia == Pascoa(dia.Year).AddDays(-2);

    /// <summary>Domingo de Páscoa (algoritmo de Meeus), base da Sexta-feira Santa.</summary>
    private static DateOnly Pascoa(int ano)
    {
        int a = ano % 19, b = ano / 100, c = ano % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = ((19 * a) + b - d - g + 15) % 30;
        int i = c / 4, k = c % 4;
        int l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        int m = (a + (11 * h) + (22 * l)) / 451;
        int mes = (h + l - (7 * m) + 114) / 31;
        int diaDoMes = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateOnly(ano, mes, diaDoMes);
    }
}

public interface IContaPagamentoRepository
{
    Task<ContaPagamento?> ObterDaFunerariaAsync(CancellationToken cancellationToken = default);

    /// <summary>Sem o filtro de tenant: é a conta do evento que diz de qual funerária ele é (COB-06).</summary>
    Task<ContaPagamento?> ObterPorIdStripeAsync(string idStripeAccount, CancellationToken cancellationToken = default);

    void Adicionar(ContaPagamento conta);
}

public interface IAssinaturaRepository
{
    Task<Assinatura?> ObterDoContratoAsync(Guid idContrato, CancellationToken cancellationToken = default);

    Task<Assinatura?> ObterPorIdAsync(Guid idAssinatura, CancellationToken cancellationToken = default);

    Task<Assinatura?> ObterPorSubscriptionAsync(string idSubscription, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Assinatura>> ListarPorStatusAsync(StatusAssinatura status, CancellationToken cancellationToken = default);

    Task<string?> ObterCustomerAsync(Guid idCliente, CancellationToken cancellationToken = default);

    void AdicionarCustomer(ClienteStripe cliente);

    void Adicionar(Assinatura assinatura);
}

public interface IFaturaRepository
{
    Task<Fatura?> ObterAsync(Guid idFatura, CancellationToken cancellationToken = default);

    Task<Fatura?> ObterPorStripeAsync(string idStripeInvoice, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Fatura>> ListarDoContratoAsync(Guid idContrato, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Fatura>> ListarEmAbertoAsync(CancellationToken cancellationToken = default);

    Task<bool> LembreteEnviadoAsync(Guid idFatura, int diasDoVencimento, CancellationToken cancellationToken = default);

    void AdicionarLembrete(LembreteFatura lembrete);

    void Adicionar(Fatura fatura);
}

public interface IEventoStripeRepository
{
    Task<EventoStripe?> ObterAsync(Guid id, CancellationToken cancellationToken = default);

    void Adicionar(EventoStripe evento);
}
