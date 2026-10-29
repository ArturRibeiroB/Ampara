using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Funeral.Application;
using Funeraria.Modules.Funeral.Application.Abstractions;
using Funeraria.Modules.Funeral.Contracts;
using Funeraria.Modules.Funeral.Application.Funerais;
using Funeraria.Modules.Funeral.Application.Itens;
using Funeraria.Modules.Funeral.Application.Locais;
using Funeraria.Modules.Funeral.Application.Preferencias;
using Funeraria.Modules.Funeral.Domain.Beneficiarios;
using Funeraria.Modules.Funeral.Domain.Funerais;
using Funeraria.Modules.Funeral.Domain.Locais;
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

        // Beneficiário, funeral e etapas (Gerar Beneficiário, Registrar Funeral, FUN-06, FUN-07).
        services.AddScoped<IBeneficiarioRepository, BeneficiarioRepository>();
        services.AddScoped<FuneralRepository>();
        services.AddScoped<IFuneralRepository>(sp => sp.GetRequiredService<FuneralRepository>());
        services.AddScoped<IFuneraisLeitura>(sp => sp.GetRequiredService<FuneralRepository>());

        // Locais, agenda e reservas (FUN-05).
        services.AddScoped<ILocalRepository, LocalRepository>();
        services.AddScoped<IReservaRepository, ReservaRepository>();
        services.AddScoped<ReservasDoFuneral>();

        // Itens do funeral: coberto x adicional e estoque (FUN-03).
        services.AddScoped<EscolhaNoCatalogo>();
        services.AddScoped<ComposicaoDosItens>();

        return services;
    }
}
