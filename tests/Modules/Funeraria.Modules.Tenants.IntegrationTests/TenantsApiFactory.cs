using Funeraria.Testing;

namespace Funeraria.Modules.Tenants.IntegrationTests;

public sealed class TenantsApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public ValueTask InitializeAsync() => IniciarAsync();
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<TenantsApiFactory>
{
    public const string Nome = "Api";
}
