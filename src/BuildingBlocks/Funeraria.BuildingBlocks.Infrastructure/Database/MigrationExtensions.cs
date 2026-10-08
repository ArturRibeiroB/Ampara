using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.BuildingBlocks.Infrastructure.Database;

/// <summary>Tipo de DbContext registrado por um módulo. Usado para aplicar as migrations de todos.</summary>
public sealed record ModuleDbContextRegistration(Type ContextType);

public static class MigrationExtensions
{
    /// <summary>
    /// Aplica as migrations de todos os módulos. Usado ao subir em desenvolvimento e nos testes de integração (INF-03).
    /// </summary>
    public static async Task ApplyModuleMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        foreach (ModuleDbContextRegistration registration in scope.ServiceProvider.GetServices<ModuleDbContextRegistration>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(registration.ContextType);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
