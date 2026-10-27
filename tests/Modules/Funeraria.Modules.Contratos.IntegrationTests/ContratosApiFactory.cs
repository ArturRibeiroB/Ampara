using System.Text.Json;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Contratos.Contracts.IntegrationEvents;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Contratos.IntegrationTests;

/// <summary>Os cenários (funerária contratável, planos, clientes) ficam em <see cref="Cenarios"/>, no Funeraria.Testing.</summary>
public sealed class ContratosApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    public ValueTask InitializeAsync() => IniciarAsync();

    protected override void ConfigurarServicos(IServiceCollection services) =>
        services.AddScoped<IIntegrationEventHandler<ContratoCriadoIntegrationEvent>, CapturarEvento<ContratoCriadoIntegrationEvent>>();
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<ContratosApiFactory>
{
    public const string Nome = "Api";
}
