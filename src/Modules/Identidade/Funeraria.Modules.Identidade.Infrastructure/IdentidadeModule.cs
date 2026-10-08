using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Identidade.Infrastructure;

public static class IdentidadeModule
{
    public static IServiceCollection AddIdentidadeModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);

        return services;
    }
}
