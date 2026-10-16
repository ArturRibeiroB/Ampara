using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using AppClaims = Funeraria.BuildingBlocks.Application.Authorization.Claims;

namespace Funeraria.Modules.Identidade.Infrastructure.Tokens;

/// <summary>Validação do JWT emitido pela Identidade (AUT-02). As policies ficam no host.</summary>
internal static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddAutenticacaoJwt(this IServiceCollection services, IConfiguration configuration)
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
                    NameClaimType = AppClaims.IdUsuario,
                    RoleClaimType = AppClaims.Perfil,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        return services;
    }
}
