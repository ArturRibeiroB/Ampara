using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using AppClaims = Funeraria.BuildingBlocks.Application.Authorization.Claims;

namespace Funeraria.Modules.Identidade.Infrastructure.Tokens;

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly JwtOptions _options = options.Value;

    public TimeSpan DuracaoRefreshToken => _options.DuracaoRefreshToken;

    public AccessToken GerarAccessToken(DadosToken dados)
    {
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        DateTime expiraEm = agora + _options.DuracaoAccessToken;

        var claims = new Dictionary<string, object>
        {
            [AppClaims.IdUsuario] = dados.IdUsuario.ToString(),
            [AppClaims.Email] = dados.Email,
            [JwtRegisteredClaimNames.Name] = dados.Nome,
            [AppClaims.Perfil] = dados.Perfil,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
        };

        AdicionarSePreenchido(claims, AppClaims.IdTenant, dados.IdTenant?.ToString());
        AdicionarSePreenchido(claims, AppClaims.IdCliente, dados.IdCliente?.ToString());
        AdicionarSePreenchido(claims, AppClaims.IdTerceiro, dados.IdTerceiro?.ToString());
        AdicionarSePreenchido(claims, AppClaims.TipoTerceiro, dados.TipoTerceiro);

        string token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expiraEm,
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ChaveAssinatura)),
                SecurityAlgorithms.HmacSha256)
        });

        return new AccessToken(token, expiraEm);
    }

    public TokenOpaco GerarTokenOpaco()
    {
        string valor = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
        return new TokenOpaco(valor, Hash(valor));
    }

    public string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static void AdicionarSePreenchido(Dictionary<string, object> claims, string nome, string? valor)
    {
        if (!string.IsNullOrEmpty(valor))
        {
            claims[nome] = valor;
        }
    }
}
