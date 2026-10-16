using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Funeraria.BuildingBlocks.Application.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Funeraria.Testing;

/// <summary>Gera JWT de teste por perfil (INF-14), assinado com a mesma chave configurada na API dos testes.</summary>
public static class TokenDeTeste
{
    public const string ChaveAssinatura = "chave-de-teste-com-pelo-menos-32-bytes!!";
    public const string Issuer = "funeraria-api";
    public const string Audience = "funeraria-web";

    public static string Gerar(string perfil, Guid? idTenant)
    {
        var claims = new List<Claim>
        {
            new(Claims.IdUsuario, Guid.NewGuid().ToString()),
            new(Claims.Email, "usuario@teste.localhost"),
            new(Claims.Perfil, perfil)
        };

        if (idTenant is not null)
        {
            claims.Add(new Claim(Claims.IdTenant, idTenant.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ChaveAssinatura)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
