using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.BuildingBlocks.Infrastructure.Seeding;

/// <summary>
/// Dados iniciais de um módulo (INF-16). Todo seeder precisa ser idempotente: roda a cada inicialização.
/// </summary>
public interface IModuleSeeder
{
    /// <summary>Ordem de execução. Ex.: a funerária de demonstração (Tenants) antes dos usuários dela (Identidade).</summary>
    int Ordem { get; }

    Task SeedAsync(CancellationToken cancellationToken);
}

public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Liga os seeders na inicialização.</summary>
    public bool Executar { get; set; }

    /// <summary>Cria a funerária de demonstração e os usuários dela. Só em desenvolvimento e testes.</summary>
    public bool Demo { get; set; }

    /// <summary>ADMIN da plataforma. E-mail e senha vêm de configuração ou secret, nunca do código.</summary>
    public string? AdminEmail { get; set; }

    public string? AdminSenha { get; set; }

    public string AdminNome { get; set; } = "Administrador da plataforma";

    /// <summary>Senha dos usuários de demonstração. Sem ela, só a funerária de demonstração é criada.</summary>
    public string? SenhaDemo { get; set; }
}

public static class SeedingExtensions
{
    public static async Task SeedModulesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        foreach (IModuleSeeder seeder in scope.ServiceProvider.GetServices<IModuleSeeder>().OrderBy(s => s.Ordem))
        {
            await seeder.SeedAsync(cancellationToken);
        }
    }
}
