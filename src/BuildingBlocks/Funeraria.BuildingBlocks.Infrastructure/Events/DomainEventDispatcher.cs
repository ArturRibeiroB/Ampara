using System.Collections.Concurrent;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.BuildingBlocks.Infrastructure.Events;

/// <summary>Entrega um evento de domínio a todos os <see cref="IDomainEventHandler{TDomainEvent}"/> registrados.</summary>
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider)
{
    private static readonly ConcurrentDictionary<Type, IHandlerInvoker> Invokers = new();

    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        IHandlerInvoker invoker = Invokers.GetOrAdd(
            domainEvent.GetType(),
            static type => (IHandlerInvoker)Activator.CreateInstance(typeof(HandlerInvoker<>).MakeGenericType(type))!);

        return invoker.InvokeAsync(serviceProvider, domainEvent, cancellationToken);
    }

    private interface IHandlerInvoker
    {
        Task InvokeAsync(IServiceProvider serviceProvider, IDomainEvent domainEvent, CancellationToken cancellationToken);
    }

    private sealed class HandlerInvoker<TDomainEvent> : IHandlerInvoker
        where TDomainEvent : IDomainEvent
    {
        public async Task InvokeAsync(IServiceProvider serviceProvider, IDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            foreach (IDomainEventHandler<TDomainEvent> handler in serviceProvider.GetServices<IDomainEventHandler<TDomainEvent>>())
            {
                await handler.Handle((TDomainEvent)domainEvent, cancellationToken);
            }
        }
    }
}
