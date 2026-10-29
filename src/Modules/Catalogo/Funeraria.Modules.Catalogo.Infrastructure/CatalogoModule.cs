using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Catalogo.Application.Abstractions;
using Funeraria.Modules.Catalogo.Contracts;
using Funeraria.Modules.Catalogo.Domain.Itens;
using Funeraria.Modules.Catalogo.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Catalogo.Infrastructure;

public static class CatalogoModule
{
    public static IServiceCollection AddCatalogoModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<CatalogoDbContext>(CatalogoDbContext.SchemaName);

        services.AddScoped<ICatalogoUnitOfWork>(sp => sp.GetRequiredService<CatalogoDbContext>());
        services.AddScoped<ICategoriaRepository, CategoriaRepository>();
        services.AddScoped<IItemCatalogoRepository, ItemCatalogoRepository>();
        services.AddScoped<CatalogoLeitura>();
        services.AddScoped<ICatalogoLeitura>(sp => sp.GetRequiredService<CatalogoLeitura>());
        services.AddScoped<ICatalogoConsultas>(sp => sp.GetRequiredService<CatalogoLeitura>());
        services.AddScoped<ICatalogoEstoque>(sp => sp.GetRequiredService<CatalogoLeitura>());

        return services;
    }
}
