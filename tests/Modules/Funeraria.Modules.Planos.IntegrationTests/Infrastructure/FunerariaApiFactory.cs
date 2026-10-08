using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Planos.Contracts.IntegrationEvents;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Modules.Tenants.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Planos.IntegrationTests.Infrastructure;

/// <summary>
/// Sobe a API inteira contra um PostgreSQL real em container (INF-14).
/// As migrations de todos os módulos são aplicadas na inicialização.
/// </summary>
public sealed class FunerariaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public EventosCapturados Eventos { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Força a criação do host (e a aplicação das migrations) antes do primeiro teste.
        _ = Services;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:ChaveAssinatura", TokenDeTeste.ChaveAssinatura);
        builder.UseSetting("Jwt:Issuer", TokenDeTeste.Issuer);
        builder.UseSetting("Jwt:Audience", TokenDeTeste.Audience);
        builder.UseSetting("Tenancy:DominioBase", "localhost");
        builder.UseSetting("Database:AplicarMigrations", "true");
        builder.UseSetting("Outbox:Intervalo", "00:00:00.200");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(Eventos);
            services.AddScoped<IIntegrationEventHandler<PlanoCriadoIntegrationEvent>, CapturarEvento<PlanoCriadoIntegrationEvent>>();
            services.AddScoped<IIntegrationEventHandler<ValorPlanoAlteradoIntegrationEvent>, CapturarEvento<ValorPlanoAlteradoIntegrationEvent>>();
        });
    }

    /// <summary>Cria uma funerária ativa com slug e CNPJ únicos e devolve o id_tenant e o slug.</summary>
    public async Task<(Guid IdTenant, string Slug)> CriarFunerariaAsync()
    {
        string slug = $"fun-{Guid.NewGuid():N}"[..20];
        string cnpj = Random.Shared.NextInt64(10_000_000_000_000, 99_999_999_999_999).ToString();

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        var funeraria = FunerariaEntity.Criar(
            $"Funerária {slug}", $"Funerária {slug} Ltda", cnpj, slug, $"{slug}@teste.localhost",
            null, OrigemCadastro.Admin, StatusFuneraria.Ativa);

        context.Add(funeraria);
        await context.SaveChangesAsync();

        return (funeraria.Id, slug);
    }
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<FunerariaApiFactory>
{
    public const string Nome = "Api";
}
