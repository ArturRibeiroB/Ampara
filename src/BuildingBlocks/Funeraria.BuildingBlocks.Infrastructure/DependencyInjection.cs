using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.BuildingBlocks.Infrastructure.Database.Interceptors;
using Funeraria.BuildingBlocks.Infrastructure.Events;
using Funeraria.BuildingBlocks.Infrastructure.Identity;
using Funeraria.BuildingBlocks.Infrastructure.Outbox;
using Funeraria.BuildingBlocks.Infrastructure.Seeding;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Funeraria.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Database";

    /// <summary>Serviços compartilhados por todos os módulos. Chamado uma vez pelo host.</summary>
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ICurrentUser, CurrentUser>();

        // Um único pool de conexões para todos os DbContexts e para o Dapper (INF-03, INF-04).
        services.AddSingleton(_ =>
        {
            string connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' não configurada.");
            return new NpgsqlDataSourceBuilder(connectionString).Build();
        });
        services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

        services.AddScoped<TenantAuditInterceptor>();
        services.AddScoped<InsertOutboxMessagesInterceptor>();
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.Section));
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.Section));

        services.AddScoped<DomainEventDispatcher>();
        services.AddScoped<IEventBus, InMemoryEventBus>();

        return services;
    }
}
