namespace Funeraria.BuildingBlocks.Domain.Results;

/// <summary>Nomes de enum no formato da API (snake_case em maiúsculas), igual ao serializador JSON.</summary>
public static class NomeApi
{
    /// <summary>EmTransito → EM_TRANSITO; APagar → A_PAGAR.</summary>
    public static string De<T>(T valor)
        where T : struct, Enum =>
        string.Concat(valor.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? $"_{c}" : c.ToString())).ToUpperInvariant();
}
