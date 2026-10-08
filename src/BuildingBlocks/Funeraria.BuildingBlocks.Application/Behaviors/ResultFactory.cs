using System.Collections.Concurrent;
using System.Reflection;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.BuildingBlocks.Application.Behaviors;

/// <summary>
/// Cria um <see cref="Result"/> ou <see cref="Result{TValue}"/> de falha quando o behavior só conhece o tipo genérico.
/// </summary>
internal static class ResultFactory
{
    private static readonly MethodInfo FailureGeneric = typeof(Result)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m is { Name: nameof(Result.Failure), IsGenericMethodDefinition: true });

    private static readonly ConcurrentDictionary<Type, Func<Error, Result>> Cache = new();

    public static TResponse Failure<TResponse>(Error error)
        where TResponse : Result
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Failure(error);
        }

        Func<Error, Result> factory = Cache.GetOrAdd(typeof(TResponse), static type =>
        {
            MethodInfo method = FailureGeneric.MakeGenericMethod(type.GetGenericArguments()[0]);
            return e => (Result)method.Invoke(null, [e])!;
        });

        return (TResponse)factory(error);
    }
}
