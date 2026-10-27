using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.Modules.Identidade.Application.Termos;
using Funeraria.Modules.Identidade.Domain.Termos;
using Funeraria.Modules.Identidade.Infrastructure.Auditoria;
using Funeraria.Modules.Identidade.Infrastructure.Termos;
using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.BuildingBlocks.Infrastructure.Seeding;
using Funeraria.Modules.Identidade.Application;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Application.Senhas;
using Funeraria.Modules.Identidade.Application.Sessoes;
using Funeraria.Modules.Identidade.Contracts;
using Funeraria.Modules.Identidade.Domain.Equipe;
using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Funeraria.Modules.Identidade.Infrastructure.Equipe;
using Funeraria.Modules.Identidade.Infrastructure.Senhas;
using Funeraria.Modules.Identidade.Infrastructure.Sessoes;
using Funeraria.Modules.Identidade.Infrastructure.Tokens;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Funeraria.Modules.Identidade.Infrastructure.Vinculos;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Identidade.Infrastructure;

public static class IdentidadeModule
{
    public static IServiceCollection AddIdentidadeModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<IdentidadeDbContext>(IdentidadeDbContext.SchemaName);

        services
            .AddIdentityCore<Usuario>(options =>
            {
                // Política de senha forte (INF-06), igual à validação do frontend.
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                // Bloqueio temporário após tentativas inválidas (AUT-02).
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                // Conta única na plataforma (DEC-02).
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddErrorDescriber<IdentityErrosPtBr>()
            .AddEntityFrameworkStores<IdentidadeDbContext>();

        services.AddAutenticacaoJwt(configuration);

        // Chaves do Data Protection no banco: todas as instâncias da API cifram e decifram com as mesmas chaves (SEG-01).
        // Em produção, proteja as chaves em repouso com certificado ou cofre (ProtectKeysWith...), conforme o provedor.
        services.AddDataProtection()
            .SetApplicationName("funeraria")
            .PersistKeysToDbContext<IdentidadeDbContext>();

        services.AddScoped<IIdentidadeUnitOfWork>(sp => sp.GetRequiredService<IdentidadeDbContext>());
        services.AddScoped<IVinculoRepository, VinculoRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUsuarios, UsuariosService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<EmissorSessao>();
        services.AddScoped<ITokenSenhaRepository, TokenSenhaRepository>();
        services.AddScoped<IFuncionarioRepository, FuncionarioRepository>();
        services.AddScoped<IAuditoria, AuditoriaService>();
        services.AddOptions<LgpdOptions>().Bind(configuration.GetSection(LgpdOptions.Section));
        services.AddSingleton<ITermosVigentes, TermosVigentes>();
        services.AddScoped<IAceiteTermosRepository, AceiteTermosRepository>();
        services.AddScoped<RegistroDeAceites>();
        services.AddOptions<IdentidadeOptions>().Bind(configuration.GetSection(IdentidadeOptions.Section));
        services.AddSingleton<ILinksIdentidade, LinksIdentidade>();
        services.AddScoped<SenhaPorLink>();
        services.AddScoped<IIdentidadeApi, IdentidadeApi>();
        services.AddScoped<IModuleSeeder, IdentidadeSeed>();
        services.AddScoped<IJobRecorrente, LimpezaSessoesJob>();

        return services;
    }
}
