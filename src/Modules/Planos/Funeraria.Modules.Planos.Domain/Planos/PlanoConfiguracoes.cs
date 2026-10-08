namespace Funeraria.Modules.Planos.Domain.Planos;

/// <summary>
/// Regras flexíveis do plano, gravadas na coluna jsonb <c>configuracoes</c> (PLA-03).
/// O que é usado em filtros e cálculos frequentes fica em coluna, e não aqui.
/// </summary>
public sealed record PlanoConfiguracoes
{
    /// <summary>Versão atual do formato. Ao mudar o formato, incremente e trate a migração dos registros antigos.</summary>
    public const int VersaoAtual = 1;

    public static readonly PlanoConfiguracoes Padrao = new();

    public int Versao { get; init; } = VersaoAtual;

    public ConfiguracaoTranslado Translado { get; init; } = new();

    public ConfiguracaoVelorio Velorio { get; init; } = new();

    public bool CremacaoInclusa { get; init; }

    public bool TransmissaoOnline { get; init; }

    public ConfiguracaoDependentes Dependentes { get; init; } = new();
}

public sealed record ConfiguracaoTranslado
{
    public int KmIncluso { get; init; }

    public bool Internacional { get; init; }
}

public sealed record ConfiguracaoVelorio
{
    public int HorasIncluidas { get; init; }

    public bool SalaVip { get; init; }
}

public sealed record ConfiguracaoDependentes
{
    public int? IdadeMaximaFilhos { get; init; }
}
