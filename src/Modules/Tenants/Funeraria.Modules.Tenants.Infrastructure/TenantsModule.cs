using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.BuildingBlocks.Infrastructure.Seeding;
using Funeraria.Modules.Tenants.Application.Abstractions;
using Funeraria.Modules.Tenants.Contracts;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Modules.Tenants.Infrastructure.Database;
using Funeraria.Modules.Tenants.Infrastructure.Funerarias;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Tenants.Infrastructure;

public static class TenantsModule
{
    public static IServiceCollection AddTenantsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<TenantsDbContext>(TenantsDbContext.SchemaName);

        services.AddMemoryCache();
        services.AddScoped<ITenantsUnitOfWork>(sp => sp.GetRequiredService<TenantsDbContext>());
        services.AddScoped<IFunerariaRepository, FunerariaRepository>();
        services.AddScoped<TenantsApi>();
        services.AddScoped<ITenantsApi>(sp => sp.GetRequiredService<TenantsApi>());
        services.AddScoped<ITenantCache>(sp => sp.GetRequiredService<TenantsApi>());
        services.AddScoped<IModuleSeeder, TenantsSeed>();

        return services;
    }
}
