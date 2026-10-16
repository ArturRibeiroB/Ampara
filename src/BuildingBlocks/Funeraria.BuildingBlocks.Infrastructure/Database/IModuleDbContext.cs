namespace Funeraria.BuildingBlocks.Infrastructure.Database;

/// <summary>
/// DbContext de um módulo. Implementado por <see cref="ModuleDbContext"/> e pelos DbContexts que não podem herdar dele,
/// como o da Identidade, que herda de <c>IdentityDbContext</c> (INF-03).
/// </summary>
public interface IModuleDbContext
{
    /// <summary>Schema do módulo no PostgreSQL. Também guarda o histórico de migrations e o outbox.</summary>
    string Schema { get; }
}
