using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Translado.Infrastructure;

public static class TransladoModule
{
    public static IServiceCollection AddTransladoModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);

        return services;
    }
}
