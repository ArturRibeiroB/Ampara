namespace Funeraria.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Confirma as alterações de um módulo numa única transação, junto com as mensagens do outbox.
/// Cada módulo declara a própria interface derivada (ex.: <c>IPlanosUnitOfWork</c>),
/// porque cada um tem o seu DbContext (INF-03).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
