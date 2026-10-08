namespace Funeraria.BuildingBlocks.Domain.Results;

/// <summary>
/// Falha de validação com as mensagens agrupadas por campo, no formato que o frontend usa para marcar o formulário.
/// </summary>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Erros)
    : Error("Validacao", "Um ou mais campos são inválidos.", ErrorType.Validacao);
