namespace Funeraria.Modules.Identidade.Infrastructure.Tokens;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "funeraria-api";

    public string Audience { get; set; } = "funeraria-web";

    /// <summary>Chave HMAC de assinatura (mínimo 32 bytes). Nunca no appsettings: user-secrets ou variável de ambiente.</summary>
    public string ChaveAssinatura { get; set; } = string.Empty;

    /// <summary>Access token de curta duração (AUT-02). Fica só em memória no frontend.</summary>
    public TimeSpan DuracaoAccessToken { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Validade do refresh token, renovada a cada rotação (AUT-03).</summary>
    public TimeSpan DuracaoRefreshToken { get; set; } = TimeSpan.FromDays(7);
}
