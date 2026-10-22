using Funeraria.BuildingBlocks.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Tenants.IntegrationTests;

/// <summary>Mudança no modelo sem migration quebra o deploy: o teste pega antes, no build local e no CI (INF-15).</summary>
[Collection(ApiCollection.Nome)]
public sealed class MigrationsTests(TenantsApiFactory factory)
{
    [Fact]
    public void Nenhum_modulo_tem_mudanca_no_modelo_sem_migration()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ModuleDbContextRegistration[] registros = [.. scope.ServiceProvider.GetServices<ModuleDbContextRegistration>()];
        Assert.NotEmpty(registros);

        string[] pendentes =
        [
            .. registros
                .Where(r => ((DbContext)scope.ServiceProvider.GetRequiredService(r.ContextType)).Database.HasPendingModelChanges())
                .Select(r => r.ContextType.Name)
        ];

        Assert.True(pendentes.Length == 0,
            $"Mudanças no modelo sem migration em: {string.Join(", ", pendentes)}. Rode 'dotnet ef migrations add' (veja o README).");
    }
}
