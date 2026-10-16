using Microsoft.AspNetCore.Http;

namespace Funeraria.Modules.Identidade.Endpoints.Auth;

/// <summary>
/// O refresh token trafega só em cookie httpOnly, Secure e SameSite=Strict, restrito às rotas de autenticação.
/// O JavaScript do frontend nunca enxerga o valor (AUT-03).
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Nome = "funeraria_refresh";
    public const string Caminho = "/api/v1/auth";

    public static string? Ler(HttpContext context) => context.Request.Cookies[Nome];

    public static void Escrever(HttpContext context, string valor, DateTime expiraEm) =>
        context.Response.Cookies.Append(Nome, valor, Opcoes(expiraEm));

    public static void Remover(HttpContext context) =>
        context.Response.Cookies.Delete(Nome, Opcoes(expiraEm: null));

    private static CookieOptions Opcoes(DateTime? expiraEm) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Caminho,
        Expires = expiraEm is null ? null : new DateTimeOffset(expiraEm.Value, TimeSpan.Zero),
        IsEssential = true
    };
}
