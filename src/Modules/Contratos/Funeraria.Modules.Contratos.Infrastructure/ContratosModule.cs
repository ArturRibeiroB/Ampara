using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Contratos.Infrastructure;

public static class ContratosModule
{
    public static IServiceCollection AddContratosModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);

        return services;
    }
}
