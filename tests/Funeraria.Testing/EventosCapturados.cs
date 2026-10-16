using System.Collections.Concurrent;
using Funeraria.BuildingBlocks.Application.Events;

namespace Funeraria.Modules.Planos.IntegrationTests.Infrastructure;

/// <summary>Guarda os eventos de integração publicados, para os testes conferirem o fluxo do outbox.</summary>
public sealed class EventosCapturados
{
    private readonly ConcurrentQueue<IIntegrationEvent> _eventos = new();

    public void Adicionar(IIntegrationEvent evento) => _eventos.Enqueue(evento);

    public async Task<TEvento> AguardarAsync<TEvento>(Func<TEvento, bool> filtro, TimeSpan? timeout = null)
        where TEvento : IIntegrationEvent
    {
        DateTime limite = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < limite)
        {
            TEvento? evento = _eventos.OfType<TEvento>().FirstOrDefault(filtro);
            if (evento is not null)
            {
                return evento;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"{typeof(TEvento).Name} não foi publicado.");
    }
}

internal sealed class CapturarEvento<TEvento>(EventosCapturados eventos) : IIntegrationEventHandler<TEvento>
    where TEvento : IIntegrationEvent
{
    public Task Handle(TEvento integrationEvent, CancellationToken cancellationToken)
    {
        eventos.Adicionar(integrationEvent);
        return Task.CompletedTask;
    }
}
