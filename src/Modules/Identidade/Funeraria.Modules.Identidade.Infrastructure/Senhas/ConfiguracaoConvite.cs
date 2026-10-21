using Funeraria.Modules.Identidade.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Funeraria.Modules.Identidade.Infrastructure.Senhas;

internal sealed class ConfiguracaoConvite(IOptions<ConviteOptions> options) : IConfiguracaoConvite
{
    private readonly ConviteOptions _options = options.Value;

    public TimeSpan ValidadeConvite => _options.ValidadeConvite;

    public string UrlDefinirSenha(string slug, string token) =>
        $"{_options.Links.UrlDefinirSenha.Replace("{slug}", slug, StringComparison.Ordinal)}?token={Uri.EscapeDataString(token)}";

    public string UrlLogin(string slug) => _options.Links.UrlLogin.Replace("{slug}", slug, StringComparison.Ordinal);
}

/// <summary>Seção <c>Identidade</c> da configuração. <c>{slug}</c> é trocado pelo subdomínio da funerária.</summary>
public sealed class ConviteOptions
{
    public const string Section = "Identidade";

    public TimeSpan ValidadeConvite { get; set; } = TimeSpan.FromDays(3);

    public LinksOptions Links { get; set; } = new();

    public sealed class LinksOptions
    {
        public string UrlDefinirSenha { get; set; } = "http://{slug}.localhost:5173/definir-senha";

        public string UrlLogin { get; set; } = "http://{slug}.localhost:5173/login";
    }
}
