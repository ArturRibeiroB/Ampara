namespace Funeraria.Modules.Tenants.Domain.Funerarias;

/// <summary>
/// Parâmetros do negócio da funerária, em jsonb tipado (TEN-03). Os padrões são os do DEC-01.
/// A tabela de preços do translado fica em tabela própria (TRA-02), porque é relacional.
/// </summary>
public sealed record ParametrosFuneraria
{
    public ParametrosCobranca Cobranca { get; init; } = new();

    public ParametrosLembretes Lembretes { get; init; } = new();

    public ParametrosTerceiros Terceiros { get; init; } = new();

    public ParametrosCertidao Certidao { get; init; } = new();
}

public sealed record ParametrosCobranca
{
    public ModoVencimento ModoVencimento { get; init; } = ModoVencimento.Fixo;

    /// <summary>Dia do vencimento quando o modo é FIXO.</summary>
    public int DiaVencimentoFixo { get; init; } = 10;

    /// <summary>Dias que o cliente pode escolher quando o modo é ESCOLHA_CLIENTE.</summary>
    public IReadOnlyList<int> DiasVencimentoPermitidos { get; init; } = [5, 10, 15, 20, 25];

    /// <summary>Dias úteis de atraso antes de suspender o plano (DEC-01, DEC-12). Pagamento em processamento não conta.</summary>
    public int ToleranciaDias { get; init; } = 1;

    /// <summary>Multa por atraso: 2%, o limite do CDC (DEC-12). Enquanto for nulo, a contratação fica bloqueada.</summary>
    public decimal? MultaPercentual { get; init; } = 2;

    /// <summary>Juros de mora: 1% ao mês, proporcional aos dias (DEC-12). Enquanto for nulo, a contratação fica bloqueada.</summary>
    public decimal? JurosMensalPercentual { get; init; } = 1;

    /// <summary>Desconto do plano anual: 10% (DEC-12). Enquanto for nulo, a contratação fica bloqueada.</summary>
    public decimal? DescontoAnualPercentual { get; init; } = 10;

    /// <summary>Reajuste no aniversário do contrato (DEC-01, COB-08).</summary>
    public decimal ReajusteAnualPercentual { get; init; } = 5;

    /// <summary>A contratação só é liberada com multa, juros e desconto anual preenchidos (TEN-03).</summary>
    public bool EstaCompleta() => MultaPercentual is not null && JurosMensalPercentual is not null && DescontoAnualPercentual is not null;

    public bool Equals(ParametrosCobranca? other) =>
        other is not null
        && ModoVencimento == other.ModoVencimento
        && DiaVencimentoFixo == other.DiaVencimentoFixo
        && DiasVencimentoPermitidos.SequenceEqual(other.DiasVencimentoPermitidos)
        && ToleranciaDias == other.ToleranciaDias
        && MultaPercentual == other.MultaPercentual
        && JurosMensalPercentual == other.JurosMensalPercentual
        && DescontoAnualPercentual == other.DescontoAnualPercentual
        && ReajusteAnualPercentual == other.ReajusteAnualPercentual;

    public override int GetHashCode() =>
        HashCode.Combine(ModoVencimento, DiaVencimentoFixo, ToleranciaDias, MultaPercentual, JurosMensalPercentual,
            DescontoAnualPercentual, ReajusteAnualPercentual);
}

public sealed record ParametrosLembretes
{
    /// <summary>Dias antes do vencimento em que o lembrete é enviado (COB-04).</summary>
    public IReadOnlyList<int> DiasAntes { get; init; } = [3];

    /// <summary>Dias depois do vencimento em que o lembrete de atraso é enviado (COB-04).</summary>
    public IReadOnlyList<int> DiasDepois { get; init; } = [1];

    public bool Equals(ParametrosLembretes? other) =>
        other is not null && DiasAntes.SequenceEqual(other.DiasAntes) && DiasDepois.SequenceEqual(other.DiasDepois);

    public override int GetHashCode() => HashCode.Combine(DiasAntes.Count, DiasDepois.Count);
}

public sealed record ParametrosTerceiros
{
    /// <summary>Horas que o terceiro tem para aceitar ou recusar um serviço (TER-02).</summary>
    public int PrazoAceiteHoras { get; init; } = 24;
}

public sealed record ParametrosCertidao
{
    /// <summary>Como a certidão de óbito é pedida (CER-02). MANUAL no MVP; SIMULADO só onde a configuração permite.</summary>
    public ModoCertidao Modo { get; init; } = ModoCertidao.Manual;
}

public enum ModoCertidao
{
    Manual = 1,
    Simulado = 2
}

public enum ModoVencimento
{
    Fixo = 1,
    EscolhaCliente = 2
}
