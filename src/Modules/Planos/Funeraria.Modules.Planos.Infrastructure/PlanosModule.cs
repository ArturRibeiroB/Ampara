using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Planos.Application;
using Funeraria.Modules.Planos.Application.Abstractions;
using Funeraria.Modules.Planos.Contracts;
using Funeraria.Modules.Planos.Domain.Planos;
using Funeraria.Modules.Planos.Infrastructure.Database;
using Funeraria.Modules.Planos.Infrastructure.Planos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Planos.Infrastructure;

public static class PlanosModule
{
    public static IServiceCollection AddPlanosModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<PlanosDbContext>(PlanosDbContext.SchemaName);

        services.AddScoped<IPlanosUnitOfWork>(sp => sp.GetRequiredService<PlanosDbContext>());
        services.AddScoped<IPlanoRepository, PlanoRepository>();
        services.AddScoped<IPlanosApi, PlanosApi>();

        return services;
    }
}
