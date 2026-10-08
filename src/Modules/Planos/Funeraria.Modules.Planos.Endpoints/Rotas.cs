namespace Funeraria.Modules.Planos.Endpoints;

internal static class Rotas
{
    public const string Tag = "Planos";

    /// <summary>Rotas públicas e do cliente, sempre no contexto da funerária do subdomínio.</summary>
    public const string Publico = "planos";

    /// <summary>Rotas da gestão da funerária (perfil FUNERARIA).</summary>
    public const string Gestao = "gestao/planos";
}
