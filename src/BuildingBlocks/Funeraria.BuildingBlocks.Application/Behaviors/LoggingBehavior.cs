using System.Diagnostics;
using Funeraria.BuildingBlocks.Domain.Results;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Funeraria.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Loga início, fim, duração e o código do erro de cada caso de uso.
/// O conteúdo da mensagem não é logado, para não vazar dados pessoais (INF-08).
/// </summary>
public sealed partial class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
    where TResponse : Result
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        string nome = typeof(TMessage).Name;
        long inicio = Stopwatch.GetTimestamp();

        TResponse resposta = await next(message, cancellationToken);

        double ms = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
        if (resposta.IsSuccess)
        {
            LogSucesso(logger, nome, ms);
        }
        else
        {
            LogFalha(logger, nome, resposta.Error.Code, ms);
        }

        return resposta;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Caso} concluído em {DuracaoMs:0.0} ms")]
    private static partial void LogSucesso(ILogger logger, string caso, double duracaoMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Caso} falhou com {CodigoErro} em {DuracaoMs:0.0} ms")]
    private static partial void LogFalha(ILogger logger, string caso, string codigoErro, double duracaoMs);
}
