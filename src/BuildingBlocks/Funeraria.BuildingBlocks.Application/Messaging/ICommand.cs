using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.BuildingBlocks.Application.Messaging;

/// <summary>Caso de uso de escrita sem valor de retorno.</summary>
public interface ICommand : Mediator.ICommand<Result>;

/// <summary>Caso de uso de escrita que devolve um valor (ex.: o id criado).</summary>
public interface ICommand<TResponse> : Mediator.ICommand<Result<TResponse>>;

public interface ICommandHandler<in TCommand> : Mediator.ICommandHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : Mediator.ICommandHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
