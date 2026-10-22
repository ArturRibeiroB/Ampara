using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Seeding;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Funeraria.Modules.Tenants.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Funeraria.Modules.Identidade.Infrastructure.Database;

/// <summary>
/// Seed da Identidade (INF-16), idempotente:
/// <list type="bullet">
/// <item>Role global ADMIN.</item>
/// <item>ADMIN da plataforma, com e-mail e senha vindos de configuração ou secret.</item>
/// <item>Em demonstração: gestor e funcionário da funerária "demo", com a senha de <c>Seed:SenhaDemo</c>.</item>
/// </list>
/// </summary>
internal sealed partial class IdentidadeSeed(
    UserManager<Usuario> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IdentidadeDbContext context,
    ITenantsApi tenants,
    IOptions<SeedOptions> options,
    TimeProvider timeProvider,
    ILogger<IdentidadeSeed> logger)
    : IModuleSeeder
{
    public const string EmailGestorDemo = "gestor@demo.localhost";
    public const string EmailFuncionarioDemo = "funcionario@demo.localhost";

    private readonly SeedOptions _options = options.Value;

    // Depois da funerária de demonstração (Tenants).
    public int Ordem => 20;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!await roleManager.RoleExistsAsync(Perfis.Admin))
        {
            Garantir(await roleManager.CreateAsync(new IdentityRole<Guid>(Perfis.Admin)), "role ADMIN");
        }

        await SeedAdminAsync();

        if (_options.Demo)
        {
            await SeedDemoAsync(cancellationToken);
        }
    }

    private async Task SeedAdminAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.AdminEmail) || string.IsNullOrWhiteSpace(_options.AdminSenha))
        {
            LogAdminNaoConfigurado(logger);
            return;
        }

        Usuario admin = await ObterOuCriarAsync(_options.AdminEmail, _options.AdminNome, _options.AdminSenha);
        if (!await userManager.IsInRoleAsync(admin, Perfis.Admin))
        {
            Garantir(await userManager.AddToRoleAsync(admin, Perfis.Admin), "role do ADMIN");
        }
    }

    private async Task SeedDemoAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SenhaDemo))
        {
            return;
        }

        FunerariaResumoDto? demo = await tenants.ObterPorSlugAsync(FunerariaDemonstracao.Slug, cancellationToken);
        if (demo is null)
        {
            return;
        }

        // O cliente de demonstração é criado pelo módulo Clientes (ClientesSeed); o terceiro, quando o módulo
        // Terceiros existir, porque o vínculo deles aponta para o cadastro nesses módulos.
        await VincularAsync(EmailGestorDemo, "Gestor Demonstração", PerfilVinculo.Funeraria, demo.IdTenant, cancellationToken);
        await VincularAsync(EmailFuncionarioDemo, "Funcionário Demonstração", PerfilVinculo.Funcionario, demo.IdTenant, cancellationToken);
    }

    private async Task VincularAsync(string email, string nome, PerfilVinculo perfil, Guid idTenant, CancellationToken cancellationToken)
    {
        Usuario usuario = await ObterOuCriarAsync(email, nome, _options.SenhaDemo!);

        if (!await context.Vinculos.AnyAsync(v => v.IdUsuario == usuario.Id && v.IdTenant == idTenant, cancellationToken))
        {
            context.Vinculos.Add(UsuarioVinculo.Criar(usuario.Id, idTenant, perfil));
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Usuario> ObterOuCriarAsync(string email, string nome, string senha)
    {
        Usuario? existente = await userManager.FindByEmailAsync(email);
        if (existente is not null)
        {
            return existente;
        }

        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Nome = nome,
            CriadoEm = timeProvider.GetUtcNow().UtcDateTime
        };

        Garantir(await userManager.CreateAsync(usuario, senha), $"usuário {email}");
        return usuario;
    }

    private static void Garantir(IdentityResult result, string oQue)
    {
        if (!result.Succeeded)
        {
            string erros = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Seed da Identidade falhou ao criar {oQue}: {erros}");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "ADMIN da plataforma não criado: configure Seed:AdminEmail e Seed:AdminSenha (user-secrets)")]
    private static partial void LogAdminNaoConfigurado(ILogger logger);
}
