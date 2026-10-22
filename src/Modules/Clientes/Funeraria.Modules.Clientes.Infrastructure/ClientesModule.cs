using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.BuildingBlocks.Infrastructure.Seeding;
using Funeraria.Modules.Clientes.Application.Abstractions;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Clientes.Domain;
using Funeraria.Modules.Clientes.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Clientes.Infrastructure;

public static class ClientesModule
{
    public static IServiceCollection AddClientesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<ClientesDbContext>(ClientesDbContext.SchemaName);

        services.AddScoped<IClientesUnitOfWork>(sp => sp.GetRequiredService<ClientesDbContext>());
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IDependenteRepository, DependenteRepository>();
        services.AddScoped<IModuleSeeder, ClientesSeed>();
        services.AddScoped<IClientesApi, ClientesApi>();

        return services;
    }
}
