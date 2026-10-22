using System.Diagnostics;
using System.Text.RegularExpressions;
using Serilog.Context;

namespace Funeraria.Api.Middlewares;

/// <summary>
/// Correlation id de cada requisição (INF-08). O frontend pode mandar o seu em <c>X-Correlation-Id</c> para juntar
/// os logs dos dois lados; sem ele, usa o trace id. O valor volta no cabeçalho da resposta e vai em todos os logs.
/// </summary>
internal sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        string? recebido = context.Request.Headers[Header];
        string correlationId = recebido is not null && Valido().IsMatch(recebido)
            ? recebido
            : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    // Só aceita um id curto e simples, para o cabeçalho não virar forma de injetar texto nos logs.
    [GeneratedRegex("^[A-Za-z0-9-_]{8,64}$")]
    private static partial Regex Valido();
}
