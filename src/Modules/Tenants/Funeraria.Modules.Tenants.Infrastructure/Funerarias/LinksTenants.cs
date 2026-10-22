using Funeraria.Modules.Tenants.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Funeraria.Modules.Tenants.Infrastructure.Funerarias;

internal sealed class LinksTenants(IOptions<ApiPublicaOptions> options) : ILinksTenants
{
    private readonly string _base = options.Value.UrlPublica.TrimEnd('/');

    public string UrlLogo(string slug, string versao) =>
        $"{_base}/api/v1/funerarias/{Uri.EscapeDataString(slug)}/logo?v={Uri.EscapeDataString(versao)}";
}

/// <summary>Seção <c>Api</c>: endereço pelo qual navegadores e clientes de e-mail alcançam a API.</summary>
public sealed class ApiPublicaOptions
{
    public const string Section = "Api";

    public string UrlPublica { get; set; } = "http://localhost:5080";
}
