namespace Funeraria.BuildingBlocks.Domain.Lgpd;

/// <summary>Valores que substituem os dados pessoais na anonimização. Sem relação com a pessoa, iguais em todos os módulos.</summary>
public static class Anonimizacao
{
    public const string Nome = "Pessoa anonimizada";

    public const string Texto = "Anonimizado";

    /// <summary>E-mail que nunca entrega (domínio .invalid, RFC 2606) e único por registro.</summary>
    public static string Email(Guid id) => $"anonimizado-{id:N}@anonimizado.invalid";

    /// <summary>
    /// Documento único por registro, para não colidir com os índices únicos pelo hash do CPF. Não é um CPF válido, então
    /// nunca coincide com o de uma pessoa real.
    /// </summary>
    public static string Documento(Guid id) => $"anon-{id:N}";

    /// <summary>Mantém só o ano: a faixa etária continua servindo aos relatórios, sem identificar a pessoa.</summary>
    public static DateOnly DataDeNascimento(DateOnly original) => new(original.Year, 1, 1);
}
