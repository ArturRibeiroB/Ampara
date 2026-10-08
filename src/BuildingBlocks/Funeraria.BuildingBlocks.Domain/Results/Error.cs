namespace Funeraria.BuildingBlocks.Domain.Results;

/// <summary>
/// Erro esperado de um caso de uso. A API converte o <see cref="Type"/> no status HTTP (INF-07).
/// </summary>
public record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Falha);

    public static Error Falha(string code, string description) => new(code, description, ErrorType.Falha);

    public static Error Validacao(string code, string description) => new(code, description, ErrorType.Validacao);

    public static Error NaoEncontrado(string code, string description) => new(code, description, ErrorType.NaoEncontrado);

    public static Error Conflito(string code, string description) => new(code, description, ErrorType.Conflito);

    public static Error Proibido(string code, string description) => new(code, description, ErrorType.Proibido);
}
