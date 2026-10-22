using Funeraria.Modules.Identidade.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Funeraria.Modules.Identidade.Infrastructure.Senhas;

internal sealed class LinksIdentidade(IOptions<IdentidadeOptions> options) : ILinksIdentidade
{
    /// <summary>Subdomínio reservado do painel da plataforma, usado nos links do ADMIN.</summary>
    private const string SlugPlataforma = "admin";

    private readonly IdentidadeOptions _options = options.Value;

    public TimeSpan ValidadeConvite => _options.ValidadeConvite;

    public TimeSpan ValidadeConfirmacaoEmail => _options.ValidadeConfirmacaoEmail;

    public TimeSpan ValidadeRedefinicaoSenha => _options.ValidadeRedefinicaoSenha;

    public string UrlDefinirSenha(string slug, string token) => ComToken(_options.Links.UrlDefinirSenha, slug, token);

    public string UrlConfirmarEmail(string slug, string token) => ComToken(_options.Links.UrlConfirmarEmail, slug, token);

    public string UrlRedefinirSenha(string? slug, string token) =>
        ComToken(_options.Links.UrlRedefinirSenha, slug ?? SlugPlataforma, token);

    public string UrlLogin(string slug) => Montar(_options.Links.UrlLogin, slug);

    private static string ComToken(string modelo, string slug, string token) =>
        $"{Montar(modelo, slug)}?token={Uri.EscapeDataString(token)}";

    private static string Montar(string modelo, string slug) => modelo.Replace("{slug}", slug, StringComparison.Ordinal);
}

/// <summary>Seção <c>Identidade</c> da configuração. <c>{slug}</c> é trocado pelo subdomínio da funerária.</summary>
public sealed class IdentidadeOptions
{
    public const string Section = "Identidade";

    public TimeSpan ValidadeConvite { get; set; } = TimeSpan.FromDays(3);

    public TimeSpan ValidadeConfirmacaoEmail { get; set; } = TimeSpan.FromDays(3);

    public TimeSpan ValidadeRedefinicaoSenha { get; set; } = TimeSpan.FromMinutes(30);

    public LinksOptions Links { get; set; } = new();

    public sealed class LinksOptions
    {
        public string UrlDefinirSenha { get; set; } = "http://{slug}.localhost:5173/definir-senha";

        public string UrlConfirmarEmail { get; set; } = "http://{slug}.localhost:5173/confirmar-email";

        public string UrlRedefinirSenha { get; set; } = "http://{slug}.localhost:5173/redefinir-senha";

        public string UrlLogin { get; set; } = "http://{slug}.localhost:5173/login";
    }
}
