using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Relatorios.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Relatorios.Infrastructure;

public static class RelatoriosModule
{
    public static IServiceCollection AddRelatoriosModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddScoped<RelatoriosDaFuneraria>();
        services.AddMemoryCache();
        services.AddSingleton<ICacheDeRelatorios, CacheDeRelatorios>();
        services.AddSingleton<IParametrosPlataforma, ParametrosPlataforma>();

        return services;
    }
}
