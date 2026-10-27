namespace Funeraria.Modules.Tenants.Contracts;

/// <summary>Funerária de demonstração criada pelo seed em desenvolvimento e testes (INF-16).</summary>
public static class FunerariaDemonstracao
{
    public const string Slug = "demo";

    public const string EmailGestor = "gestor@demo.localhost";

    /// <summary>Contas dos terceiros de demonstração, um de cada tipo (FROTA, CARTORIO_DESPACHANTE, FORNECEDOR).</summary>
    public const string EmailFrota = "frota@demo.localhost";

    public const string EmailDespachante = "despachante@demo.localhost";

    public const string EmailFornecedor = "fornecedor@demo.localhost";

    /// <summary>A demo já começa com os planos e o catálogo modelo ativos, pronta para vender.</summary>
    public static bool E(string slug) => string.Equals(slug, Slug, StringComparison.OrdinalIgnoreCase);
}
