using System.Text;
using Funeraria.BuildingBlocks.Application.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Funeraria.Api.Identity;

/// <summary>
/// Validação do JWT e policies da matriz do DEC-06 (AUT-02, AUT-04).
/// A emissão dos tokens (login, refresh) fica no módulo Identidade.
/// </summary>
internal static class AuthenticationExtensions
{
    public static IServiceCollection AddAutenticacao(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .Validate(o => Encoding.UTF8.GetByteCount(o.ChaveAssinatura) >= 32,
                "Jwt:ChaveAssinatura não configurada ou com menos de 32 bytes. Configure via user-secrets.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                JwtOptions jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.ChaveAssinatura)),
                    NameClaimType = Claims.IdUsuario,
                    RoleClaimType = Claims.Perfil,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.Plataforma, p => p.RequireRole(Perfis.Admin))
            .AddPolicy(Politicas.GestaoFuneraria, p => p
                .RequireRole(Perfis.Funeraria)
                .RequireClaim(Claims.IdTenant))
            .AddPolicy(Politicas.EquipeFuneraria, p => p
                .RequireRole(Perfis.Funeraria, Perfis.Funcionario)
                .RequireClaim(Claims.IdTenant))
            .AddPolicy(Politicas.Cliente, p => p
                .RequireRole(Perfis.Cliente)
                .RequireClaim(Claims.IdTenant)
                .RequireClaim(Claims.IdCliente))
            .AddPolicy(Politicas.TerceiroFrota, p => Terceiro(p, TiposTerceiro.Frota))
            .AddPolicy(Politicas.TerceiroDespachante, p => Terceiro(p, TiposTerceiro.CartorioDespachante))
            .AddPolicy(Politicas.TerceiroFornecedor, p => Terceiro(p, TiposTerceiro.Fornecedor));

        return services;
    }

    private static void Terceiro(Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder policy, string tipo) =>
        policy
            .RequireRole(Perfis.Terceiro)
            .RequireClaim(Claims.IdTenant)
            .RequireClaim(Claims.IdTerceiro)
            .RequireClaim(Claims.TipoTerceiro, tipo);
}

internal sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "funeraria-api";

    public string Audience { get; set; } = "funeraria-web";

    /// <summary>Chave HMAC de assinatura (mínimo 32 bytes). Nunca no appsettings: user-secrets ou variável de ambiente.</summary>
    public string ChaveAssinatura { get; set; } = string.Empty;
}
