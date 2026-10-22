using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Contratos.Application;
using Funeraria.Modules.Contratos.Application.Abstractions;
using Funeraria.Modules.Contratos.Contracts;
using Funeraria.Modules.Contratos.Domain.Contratos;
using Funeraria.Modules.Contratos.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Contratos.Infrastructure;

public static class ContratosModule
{
    public static IServiceCollection AddContratosModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<ContratosDbContext>(ContratosDbContext.SchemaName);

        services.AddScoped<IContratosUnitOfWork>(sp => sp.GetRequiredService<ContratosDbContext>());
        services.AddScoped<IContratoRepository, ContratoRepository>();
        services.AddScoped<IContratosApi, ContratosApi>();

        return services;
    }
}
