using Funeraria.BuildingBlocks.Infrastructure.Http;
using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Translado.Application.Translados;
using Funeraria.Modules.Translado.Domain.Translados;
using Funeraria.Modules.Translado.Infrastructure.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Translado.Infrastructure;

public static class TransladoModule
{
    public static IServiceCollection AddTransladoModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<TransladoDbContext>(TransladoDbContext.SchemaName);
        services.AddOptions<OpenRouteServiceOptions>().Bind(configuration.GetSection(OpenRouteServiceOptions.Section));

        services.AddScoped<ITransladoUnitOfWork>(sp => sp.GetRequiredService<TransladoDbContext>());
        services.AddScoped<ITransladoRepository, TransladoRepository>();
        services.AddScoped<CotacaoDeTranslado>();
        services.AddScoped<AcessoAoTranslado>();
        services.AddScoped<DocumentosDoTranslado>();
        services.AddScoped<MudancaDeStatus>();
        services.AddScoped<IEntregaStatusTranslado, EntregaStatusSignalR>();
        services.AddClienteExterno<IDistanciaGateway, OpenRouteServiceGateway>(configuration, _ => { });

        return services;
    }

    /// <summary>Hub em <c>/hubs/translados</c> (TRA-06), fora do prefixo /api/v1.</summary>
    public static IEndpointRouteBuilder MapTransladosHub(this IEndpointRouteBuilder app)
    {
        app.MapHub<TransladosHub>(TransladosHub.Caminho);
        return app;
    }
}
