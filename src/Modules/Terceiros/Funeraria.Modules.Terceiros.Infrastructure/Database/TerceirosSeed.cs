using Funeraria.BuildingBlocks.Infrastructure.Seeding;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Terceiros.Application.Cadastro;
using Funeraria.Modules.Terceiros.Domain.Terceiros;
using Funeraria.Modules.Tenants.Contracts;
using Mediator;
using Microsoft.Extensions.Options;

namespace Funeraria.Modules.Terceiros.Infrastructure.Database;

/// <summary>
/// Um terceiro de cada tipo na funerária "demo" (INF-16). Idempotente. Passa pelo mesmo cadastro da gestão: as contas já
/// existem com a senha de <c>Seed:SenhaDemo</c> (IdentidadeSeed), então o vínculo nasce ativo e eles já podem entrar.
/// </summary>
internal sealed class TerceirosSeed(
    TerceiroRepository terceiros,
    ITenantsApi tenants,
    TenantContext tenantContext,
    ISender sender,
    IOptions<SeedOptions> options)
    : IModuleSeeder
{
    private static readonly (TipoTerceiro Tipo, string Email, string Nome, string Cnpj)[] Demo =
    [
        (TipoTerceiro.Frota, FunerariaDemonstracao.EmailFrota, "Translados Demonstração Ltda", "11222333000262"),
        (TipoTerceiro.CartorioDespachante, FunerariaDemonstracao.EmailDespachante, "Despachante Demonstração Ltda", "44555666000181"),
        (TipoTerceiro.Fornecedor, FunerariaDemonstracao.EmailFornecedor, "Flores Demonstração Ltda", "77888999000181")
    ];

    // Depois da funerária (Tenants, 10) e das contas de demonstração (Identidade, 20).
    public int Ordem => 40;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        SeedOptions seed = options.Value;
        if (!seed.Demo || string.IsNullOrWhiteSpace(seed.SenhaDemo))
        {
            return;
        }

        FunerariaResumoDto? demo = await tenants.ObterPorSlugAsync(FunerariaDemonstracao.Slug, cancellationToken);
        if (demo is null)
        {
            return;
        }

        // O seed roda fora de uma requisição: o tenant precisa ser definido para o filtro global e o cadastro.
        tenantContext.Set(demo.IdTenant);

        foreach ((TipoTerceiro tipo, string email, string nome, string cnpj) in Demo)
        {
            if (await terceiros.ExisteEmailAsync(email, cancellationToken))
            {
                continue;
            }

            var resultado = await sender.Send(
                new CadastrarTerceiroCommand(tipo, email, nome, cnpj, "(11) 3333-0000", null, null), cancellationToken);
            if (resultado.IsFailure)
            {
                throw new InvalidOperationException($"Seed do terceiro de demonstração {email} falhou: {resultado.Error.Description}");
            }
        }
    }
}
