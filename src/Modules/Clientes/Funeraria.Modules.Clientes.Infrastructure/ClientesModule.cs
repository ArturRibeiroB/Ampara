using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Clientes.Infrastructure;

public static class ClientesModule
{
    public static IServiceCollection AddClientesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);

        return services;
    }
}
