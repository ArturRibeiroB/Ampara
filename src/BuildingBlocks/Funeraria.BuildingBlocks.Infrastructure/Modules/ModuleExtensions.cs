using System.Reflection;
using FluentValidation;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.BuildingBlocks.Infrastructure.Database.Interceptors;
using Funeraria.BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Funeraria.BuildingBlocks.Infrastructure.Modules;

/// <summary>Registros comuns que cada módulo faz no seu <c>AddXModule()</c>.</summary>
public static class ModuleExtensions
{
    private static readonly Type[] HandlerInterfaces = [typeof(IDomainEventHandler<>), typeof(IIntegrationEventHandler<>)];

    /// <summary>
    /// Registra os validadores e os handlers de eventos da camada Application do módulo.
    /// Os handlers de commands e queries são registrados pelo Mediator (source generator) no host.
    /// </summary>
    public static IServiceCollection AddModuleApplication(this IServiceCollection services, Assembly applicationAssembly)
    {
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        foreach (Type tipo in applicationAssembly.DefinedTypes.Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (Type contrato in tipo.GetInterfaces().Where(i =>
                         i.IsGenericType && HandlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            {
                services.AddScoped(contrato, tipo);
            }
        }

        return services;
    }

    /// <summary>
    /// Registra o DbContext do módulo no seu schema, com snake_case, interceptors de tenant/auditoria/outbox,
    /// histórico de migrations no próprio schema e o processador do outbox (INF-03).
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : ModuleDbContext
    {
        services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(
                sp.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                sp.GetRequiredService<TenantAuditInterceptor>(),
                sp.GetRequiredService<InsertOutboxMessagesInterceptor>()));

        services.AddSingleton(new ModuleDbContextRegistration(typeof(TContext)));
        services.AddSingleton<OutboxProcessor<TContext>>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxProcessor<TContext>>());

        return services;
    }
}
