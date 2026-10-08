using System.Text.Json;
using FluentValidation;
using Funeraria.BuildingBlocks.Domain.Results;
using Mediator;

namespace Funeraria.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Roda os validadores do FluentValidation antes do handler. Falha de validação vira
/// <see cref="ValidationError"/> dentro do Result, sem lançar exceção (INF-05, INF-07).
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
    where TResponse : Result
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        IValidator<TMessage>[] validadores = validators as IValidator<TMessage>[] ?? [.. validators];
        if (validadores.Length == 0)
        {
            return await next(message, cancellationToken);
        }

        var contexto = new ValidationContext<TMessage>(message);
        var resultados = await Task.WhenAll(validadores.Select(v => v.ValidateAsync(contexto, cancellationToken)));

        Dictionary<string, string[]> erros = resultados
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .GroupBy(f => ToJsonName(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

        if (erros.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        return ResultFactory.Failure<TResponse>(new ValidationError(erros));
    }

    // "Configuracoes.Translado.KmIncluso" -> "configuracoes.translado.km_incluso", igual ao JSON da API.
    private static string ToJsonName(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.SnakeCaseLower.ConvertName));
}
