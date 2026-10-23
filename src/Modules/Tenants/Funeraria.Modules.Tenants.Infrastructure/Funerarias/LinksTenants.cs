using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.Modules.Tenants.Application.Abstractions;

namespace Funeraria.Modules.Tenants.Infrastructure.Funerarias;

internal sealed class LinksTenants(IUrlPublicaApi urlPublica) : ILinksTenants
{
    public string UrlLogo(string slug, string versao) =>
        urlPublica.Montar($"/api/v1/funerarias/{Uri.EscapeDataString(slug)}/logo?v={Uri.EscapeDataString(versao)}");
}
