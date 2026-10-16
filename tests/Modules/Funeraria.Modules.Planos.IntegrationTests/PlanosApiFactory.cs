using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Planos.Contracts.IntegrationEvents;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Planos.IntegrationTests;

public sealed class PlanosApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public ValueTask InitializeAsync() => IniciarAsync();

    protected override void ConfigurarServicos(IServiceCollection services)
    {
        services.AddScoped<IIntegrationEventHandler<PlanoCriadoIntegrationEvent>, CapturarEvento<PlanoCriadoIntegrationEvent>>();
        services.AddScoped<IIntegrationEventHandler<ValorPlanoAlteradoIntegrationEvent>, CapturarEvento<ValorPlanoAlteradoIntegrationEvent>>();
    }
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<PlanosApiFactory>
{
    public const string Nome = "Api";
}
