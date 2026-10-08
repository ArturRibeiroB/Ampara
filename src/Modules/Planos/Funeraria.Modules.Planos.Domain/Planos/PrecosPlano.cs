namespace Funeraria.Modules.Planos.Domain.Planos;

/// <summary>Valores do plano por periodicidade, para o titular e por dependente (DEC-01).</summary>
public sealed record PrecosPlano(
    decimal ValorMensal,
    decimal ValorAnual,
    decimal ValorDependenteMensal,
    decimal ValorDependenteAnual)
{
    public decimal Calcular(Periodicidade periodicidade, int quantidadeDependentes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantidadeDependentes);

        return periodicidade switch
        {
            Periodicidade.Mensal => ValorMensal + (ValorDependenteMensal * quantidadeDependentes),
            Periodicidade.Anual => ValorAnual + (ValorDependenteAnual * quantidadeDependentes),
            _ => throw new ArgumentOutOfRangeException(nameof(periodicidade), periodicidade, null)
        };
    }
}
