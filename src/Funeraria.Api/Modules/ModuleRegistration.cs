using Funeraria.BuildingBlocks.Application.Behaviors;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Catalogo.Infrastructure;
using Funeraria.Modules.Clientes.Infrastructure;
using Funeraria.Modules.Contratos.Infrastructure;
using Funeraria.Modules.Documentos.Infrastructure;
using Funeraria.Modules.Financeiro.Infrastructure;
using Funeraria.Modules.Funeral.Infrastructure;
using Funeraria.Modules.Identidade.Infrastructure;
using Funeraria.Modules.Notificacoes.Infrastructure;
using Funeraria.Modules.Planos.Infrastructure;
using Funeraria.Modules.Relatorios.Infrastructure;
using Funeraria.Modules.Tenants.Infrastructure;
using Funeraria.Modules.Terceiros.Infrastructure;
using Funeraria.Modules.Translado.Infrastructure;

namespace Funeraria.Api.Modules;

/// <summary>
/// Composição dos módulos. O host só registra e mapeia: nenhuma regra de negócio mora aqui (INF-01).
/// Ao criar um módulo, adicione-o nas três listas abaixo e no mapa de módulos do README.
/// </summary>
internal static class ModuleRegistration
{
    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddIdentidadeModule(configuration)
            .AddTenantsModule(configuration)
            .AddClientesModule(configuration)
            .AddPlanosModule(configuration)
            .AddCatalogoModule(configuration)
            .AddContratosModule(configuration)
            .AddFinanceiroModule(configuration)
            .AddFuneralModule(configuration)
            .AddDocumentosModule(configuration)
            .AddTransladoModule(configuration)
            .AddTerceirosModule(configuration)
            .AddNotificacoesModule(configuration)
            .AddRelatoriosModule(configuration);

        services
            .AddEndpoints(Funeraria.Modules.Identidade.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Tenants.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Clientes.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Planos.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Catalogo.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Contratos.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Financeiro.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Funeral.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Documentos.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Translado.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Terceiros.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Notificacoes.Endpoints.AssemblyReference.Assembly)
            .AddEndpoints(Funeraria.Modules.Relatorios.Endpoints.AssemblyReference.Assembly);

        // O Mediator gera o registro dos handlers no build, varrendo os assemblies referenciados pelo host.
        // Por isso os valores aqui precisam ser constantes de compilação e os handlers precisam ser públicos.
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.GenerateTypesAsInternal = true;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
                typeof(ValidationBehavior<,>)
            ];
        });

        return services;
    }

    /// <summary>Mapeia os endpoints de todos os módulos sob /api/v1 (INF-09).</summary>
    public static IEndpointRouteBuilder MapModuleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/v1").MapEndpoints();
        app.MapNotificacoesHub();
        return app;
    }
}
