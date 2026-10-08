using Funeraria.BuildingBlocks.Domain.Results;
using Microsoft.AspNetCore.Http;

namespace Funeraria.BuildingBlocks.Infrastructure.Endpoints;

/// <summary>
/// Converte um <see cref="Result"/> de falha em ProblemDetails (INF-07):
/// Validacao → 400, Proibido → 403, NaoEncontrado → 404, Conflito → 409, Falha → 400.
/// O traceId é incluído pela configuração de ProblemDetails do host.
/// </summary>
public static class ApiResults
{
    public static IResult Problem(Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Não é possível gerar ProblemDetails para um resultado de sucesso.");
        }

        Error error = result.Error;
        (int status, string title) = error.Type switch
        {
            ErrorType.Validacao => (StatusCodes.Status400BadRequest, "Requisição inválida"),
            ErrorType.Proibido => (StatusCodes.Status403Forbidden, "Proibido"),
            ErrorType.NaoEncontrado => (StatusCodes.Status404NotFound, "Não encontrado"),
            ErrorType.Conflito => (StatusCodes.Status409Conflict, "Conflito"),
            _ => (StatusCodes.Status400BadRequest, "Falha")
        };

        IReadOnlyDictionary<string, string[]> erros = error is ValidationError validation
            ? validation.Erros
            : new Dictionary<string, string[]>();

        return Results.Problem(
            statusCode: status,
            title: title,
            detail: error.Description,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code,
                ["errors"] = erros
            });
    }

    public static IResult Match<TValue>(this Result<TValue> result, Func<TValue, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : Problem(result);

    public static IResult Match(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : Problem(result);
}
