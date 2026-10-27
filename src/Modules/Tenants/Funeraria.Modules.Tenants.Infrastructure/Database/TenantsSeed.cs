using Funeraria.BuildingBlocks.Infrastructure.Seeding;
using Funeraria.Modules.Tenants.Contracts;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Infrastructure.Database;

/// <summary>
/// Funerária de demonstração, só para desenvolvimento e testes (INF-16). Idempotente.
/// Nasce pelo mesmo cadastro das outras: o evento FunerariaCriada copia os planos e o catálogo modelo (já ativos na demo).
/// Os seeds rodam antes do outbox: quando o evento chega, a Identidade já criou o gestor com senha e o vínculo.
/// </summary>
internal sealed class TenantsSeed(TenantsDbContext context, IOptions<SeedOptions> options) : IModuleSeeder
{
    public int Ordem => 10;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Demo || await context.Funerarias.AnyAsync(f => f.Slug == FunerariaDemonstracao.Slug, cancellationToken))
        {
            return;
        }

        var demo = FunerariaEntity.Cadastrar(
            nomeFantasia: "Funerária Demonstração",
            razaoSocial: "Funerária Demonstração Ltda",
            cnpj: "11222333000181",
            slug: FunerariaDemonstracao.Slug,
            emailContato: "contato@demo.localhost",
            telefone: null,
            origem: OrigemCadastro.Admin,
            gestorNome: "Gestor Demonstração",
            gestorEmail: FunerariaDemonstracao.EmailGestor);
        demo.AlterarStatus(StatusFuneraria.Ativa);
        context.Funerarias.Add(demo);

        await context.SaveChangesAsync(cancellationToken);
    }
}
