using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Modules.Tenants.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Testing;

/// <summary>
/// Sobe a API inteira contra um PostgreSQL real em container (INF-14). As migrations de todos os módulos
/// e o seed (só o ADMIN, sem dados de demonstração) rodam na inicialização.
/// </summary>
/// <remarks>
/// Cada projeto de testes de integração cria uma subclasse que implementa <c>IAsyncLifetime</c> chamando
/// <see cref="IniciarAsync"/>, e pode registrar serviços próprios em <see cref="ConfigurarServicos"/>.
/// </remarks>
public abstract class FunerariaApiFactory : WebApplicationFactory<Program>
{
    public const string EmailAdmin = "admin@teste.localhost";
    public const string SenhaPadrao = "Senha@123";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public EventosCapturados Eventos { get; } = new();

    public EmailsCapturados Emails { get; } = new();

    public async ValueTask IniciarAsync()
    {
        await _postgres.StartAsync();

        // Força a criação do host (migrations e seed) antes do primeiro teste.
        _ = Services;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>Serviços extras do projeto de testes, como handlers que capturam eventos de integração.</summary>
    protected virtual void ConfigurarServicos(IServiceCollection services)
    {
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
        builder.UseSetting("Seed:Executar", "true");
        builder.UseSetting("Seed:AdminEmail", EmailAdmin);
        builder.UseSetting("Seed:AdminSenha", SenhaPadrao);
        builder.UseSetting("Outbox:Intervalo", "00:00:00.200");
        builder.UseSetting("LimiteRequisicoes:Onboarding:Limite", "100000");
        builder.UseSetting("LimiteRequisicoes:CadastroCliente:Limite", "100000");
        builder.UseSetting("LimiteRequisicoes:Autenticacao:Limite", "100000");
        builder.UseSetting("Seguranca:ForcarHttps", "false");
        builder.UseSetting("Identidade:Links:UrlDefinirSenha", "http://{slug}.teste.localhost/definir-senha");
        builder.UseSetting("Identidade:Links:UrlLogin", "http://{slug}.teste.localhost/login");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(Eventos);
            services.AddSingleton<IEmailSender>(Emails);
            ConfigurarServicos(services);
        });
    }

    /// <summary>Cria uma funerária ativa com slug e CNPJ únicos e devolve o id_tenant e o slug.</summary>
    public async Task<(Guid IdTenant, string Slug)> CriarFunerariaAsync(StatusFuneraria status = StatusFuneraria.Ativa)
    {
        string slug = $"fun-{Guid.NewGuid():N}"[..20];
        string cnpj = DocumentosDeTeste.NovoCnpj();

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        var funeraria = FunerariaEntity.Criar(
            $"Funerária {slug}", $"Funerária {slug} Ltda", cnpj, slug, $"{slug}@teste.localhost",
            null, OrigemCadastro.Admin, status);

        context.Add(funeraria);
        await context.SaveChangesAsync();

        return (funeraria.Id, slug);
    }
}
