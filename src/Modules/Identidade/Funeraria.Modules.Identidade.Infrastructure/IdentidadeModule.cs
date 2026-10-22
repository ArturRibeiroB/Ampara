using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.BuildingBlocks.Infrastructure.Seeding;
using Funeraria.Modules.Identidade.Application;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Application.Senhas;
using Funeraria.Modules.Identidade.Application.Sessoes;
using Funeraria.Modules.Identidade.Contracts;
using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Funeraria.Modules.Identidade.Infrastructure.Senhas;
using Funeraria.Modules.Identidade.Infrastructure.Sessoes;
using Funeraria.Modules.Identidade.Infrastructure.Tokens;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Funeraria.Modules.Identidade.Infrastructure.Vinculos;
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

        services.AddScoped<IIdentidadeUnitOfWork>(sp => sp.GetRequiredService<IdentidadeDbContext>());
        services.AddScoped<IVinculoRepository, VinculoRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUsuarios, UsuariosService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<EmissorSessao>();
        services.AddScoped<ITokenSenhaRepository, TokenSenhaRepository>();
        services.AddOptions<IdentidadeOptions>().Bind(configuration.GetSection(IdentidadeOptions.Section));
        services.AddSingleton<ILinksIdentidade, LinksIdentidade>();
        services.AddScoped<SenhaPorLink>();
        services.AddScoped<IIdentidadeApi, IdentidadeApi>();
        services.AddScoped<IModuleSeeder, IdentidadeSeed>();
        services.AddScoped<IJobRecorrente, LimpezaSessoesJob>();

        return services;
    }
}
