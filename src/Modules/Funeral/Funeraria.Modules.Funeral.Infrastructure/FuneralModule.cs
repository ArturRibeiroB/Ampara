using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Funeral.Infrastructure;

public static class FuneralModule
{
    public static IServiceCollection AddFuneralModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);

        return services;
    }
}
