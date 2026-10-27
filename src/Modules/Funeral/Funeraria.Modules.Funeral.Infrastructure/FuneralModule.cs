using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Funeral.Application;
using Funeraria.Modules.Funeral.Application.Abstractions;
using Funeraria.Modules.Funeral.Contracts;
using Funeraria.Modules.Funeral.Application.Preferencias;
using Funeraria.Modules.Funeral.Domain.Preferencias;
using Funeraria.Modules.Funeral.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Funeral.Infrastructure;

public static class FuneralModule
{
    public static IServiceCollection AddFuneralModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<FuneralDbContext>(FuneralDbContext.SchemaName);

        services.AddScoped<IFuneralUnitOfWork>(sp => sp.GetRequiredService<FuneralDbContext>());
        services.AddScoped<IPreferenciaRepository, PreferenciaRepository>();
        services.AddScoped<PessoasDoTitular>();
        services.AddScoped<MontagemPreferencia>();
        services.AddScoped<IFuneralApi, FuneralApi>();

        return services;
    }
}
