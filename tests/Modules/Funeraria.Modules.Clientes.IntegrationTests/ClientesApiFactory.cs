using Funeraria.Testing;

namespace Funeraria.Modules.Clientes.IntegrationTests;

public sealed class ClientesApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public ValueTask InitializeAsync() => IniciarAsync();
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<ClientesApiFactory>
{
    public const string Nome = "Api";
}
