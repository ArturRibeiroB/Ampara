using Funeraria.Modules.Tenants.Domain.Funerarias;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Infrastructure.Database;

/// <summary>Funerária de demonstração, só para desenvolvimento e testes (INF-16). Idempotente.</summary>
public static class TenantsSeed
{
    public const string SlugDemo = "demo";

    public static async Task SeedDemoAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();

        if (await context.Funerarias.AnyAsync(f => f.Slug == SlugDemo, cancellationToken))
        {
            return;
        }

        context.Funerarias.Add(FunerariaEntity.Criar(
            nomeFantasia: "Funerária Demonstração",
            razaoSocial: "Funerária Demonstração Ltda",
            cnpj: "11222333000181",
            slug: SlugDemo,
            emailContato: "contato@demo.localhost",
            telefone: null,
            origem: OrigemCadastro.Admin,
            status: StatusFuneraria.Ativa));

        await context.SaveChangesAsync(cancellationToken);
    }
}
