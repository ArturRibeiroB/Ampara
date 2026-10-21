using Funeraria.BuildingBlocks.Infrastructure.Seeding;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Clientes.Application.Commands.CadastrarCliente;
using Funeraria.Modules.Tenants.Contracts;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Funeraria.Modules.Clientes.Infrastructure.Database;

/// <summary>
/// Cliente de demonstração da funerária "demo" (INF-16), com a senha de <c>Seed:SenhaDemo</c>. Idempotente.
/// Passa pelo mesmo caso de uso do cadastro, então já sai com a conta e o vínculo CLIENTE.
/// </summary>
internal sealed class ClientesSeed(
    ClientesDbContext context,
    ITenantsApi tenants,
    TenantContext tenantContext,
    ISender sender,
    IOptions<SeedOptions> options)
    : IModuleSeeder
{
    public const string EmailClienteDemo = "cliente@demo.localhost";

    // Depois da funerária (Tenants, 10) e da equipe de demonstração (Identidade, 20).
    public int Ordem => 30;

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

        if (await context.Clientes.AnyAsync(c => c.Email == EmailClienteDemo, cancellationToken))
        {
            return;
        }

        var resultado = await sender.Send(new CadastrarClienteCommand(
            "Cliente Demonstração",
            "529.982.247-25",
            new DateOnly(1980, 5, 10),
            EmailClienteDemo,
            seed.SenhaDemo,
            "(11) 99999-0000",
            "Rua das Flores, 100",
            "01001-000",
            "São Paulo",
            "SP"), cancellationToken);

        if (resultado.IsFailure)
        {
            throw new InvalidOperationException($"Seed do cliente de demonstração falhou: {resultado.Error.Description}");
        }
    }
}
