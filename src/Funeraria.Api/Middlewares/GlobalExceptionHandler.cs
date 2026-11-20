using Funeraria.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Api.Middlewares;

/// <summary>
/// Falhas inesperadas viram ProblemDetails 500, sem stack trace fora de Development (INF-07).
/// Erros esperados não passam por aqui: os handlers devolvem Result.
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            BadHttpRequestException bad => new ProblemDetails
            {
                Status = bad.StatusCode,
                Title = "Requisição inválida",
                Detail = "O corpo ou os parâmetros da requisição estão em formato inválido.",
                Extensions = { ["code"] = "Requisicao.Invalida", ["errors"] = new Dictionary<string, string[]>() }
            },
            ViolacaoDeUnicidadeException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflito",
                Detail = "Já existe um registro com esses dados. Recarregue e confira antes de tentar de novo.",
                Extensions = { ["code"] = "Registro.Duplicado", ["errors"] = new Dictionary<string, string[]>() }
            },
            ImpasseNoBancoException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflito",
                Detail = "Outra operação alterou os mesmos dados ao mesmo tempo. Tente novamente.",
                Extensions = { ["code"] = "Concorrencia", ["errors"] = new Dictionary<string, string[]>() }
            },
            DbUpdateConcurrencyException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflito",
                Detail = "O registro foi alterado por outra pessoa. Recarregue e tente novamente.",
                Extensions = { ["code"] = "Concorrencia", ["errors"] = new Dictionary<string, string[]>() }
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro interno",
                Detail = environment.IsDevelopment() ? exception.ToString() : "Ocorreu um erro inesperado.",
                Extensions = { ["code"] = "Erro.Interno", ["errors"] = new Dictionary<string, string[]>() }
            }
        };

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            LogErroNaoTratado(logger, exception);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Erro não tratado")]
    private static partial void LogErroNaoTratado(ILogger logger, Exception exception);
}
