using Funeraria.BuildingBlocks.Infrastructure.Seeding;
using Funeraria.Modules.Tenants.Contracts;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Infrastructure.Database;

/// <summary>Funerária de demonstração, só para desenvolvimento e testes (INF-16). Idempotente.</summary>
internal sealed class TenantsSeed(TenantsDbContext context, IOptions<SeedOptions> options) : IModuleSeeder
{
    public int Ordem => 10;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Demo || await context.Funerarias.AnyAsync(f => f.Slug == FunerariaDemonstracao.Slug, cancellationToken))
        {
            return;
        }

        context.Funerarias.Add(FunerariaEntity.Criar(
            nomeFantasia: "Funerária Demonstração",
            razaoSocial: "Funerária Demonstração Ltda",
            cnpj: "11222333000181",
            slug: FunerariaDemonstracao.Slug,
            emailContato: "contato@demo.localhost",
            telefone: null,
            origem: OrigemCadastro.Admin,
            status: StatusFuneraria.Ativa));

        await context.SaveChangesAsync(cancellationToken);
    }
}
