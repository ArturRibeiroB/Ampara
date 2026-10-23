using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Terceiros.Application.Abstractions;
using Funeraria.Modules.Terceiros.Domain.Terceiros;
using Funeraria.Modules.Terceiros.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Terceiros.Infrastructure;

public static class TerceirosModule
{
    public static IServiceCollection AddTerceirosModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<TerceirosDbContext>(TerceirosDbContext.SchemaName);

        services.AddScoped<ITerceirosUnitOfWork>(sp => sp.GetRequiredService<TerceirosDbContext>());
        services.AddScoped<TerceiroRepository>();
        services.AddScoped<ITerceiroRepository>(sp => sp.GetRequiredService<TerceiroRepository>());
        services.AddScoped<ITerceirosLeitura>(sp => sp.GetRequiredService<TerceiroRepository>());

        return services;
    }
}
