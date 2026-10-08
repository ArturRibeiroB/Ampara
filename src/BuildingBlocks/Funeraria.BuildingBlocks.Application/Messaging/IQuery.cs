using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.BuildingBlocks.Application.Messaging;

/// <summary>Caso de uso de leitura. Não altera estado.</summary>
public interface IQuery<TResponse> : Mediator.IQuery<Result<TResponse>>;

public interface IQueryHandler<in TQuery, TResponse> : Mediator.IQueryHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
