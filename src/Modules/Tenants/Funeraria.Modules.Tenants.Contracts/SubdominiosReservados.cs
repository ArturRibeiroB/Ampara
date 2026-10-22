namespace Funeraria.Modules.Tenants.Contracts;

/// <summary>
/// Subdomínios da plataforma que não podem ser slug de funerária (TEN-02). Usado no cadastro e pelo host
/// ao resolver o tenant: uma requisição em <c>api.funeraria.app</c> não é de nenhuma funerária.
/// </summary>
public static class SubdominiosReservados
{
    private static readonly HashSet<string> Nomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "app", "www", "web", "mail", "email", "smtp", "ftp", "cdn", "static", "assets",
        "status", "suporte", "ajuda", "blog", "docs", "login", "auth", "cadastro", "onboarding",
        "gestao", "terceiros", "plataforma", "funeraria", "funerarias"
    };

    public static bool Contem(string slug) => Nomes.Contains(slug);
}
