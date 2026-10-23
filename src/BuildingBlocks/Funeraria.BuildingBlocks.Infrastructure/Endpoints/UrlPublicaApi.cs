using Funeraria.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Funeraria.BuildingBlocks.Infrastructure.Endpoints;

internal sealed class UrlPublicaApi(IOptions<ApiPublicaOptions> options) : IUrlPublicaApi
{
    private readonly string _base = options.Value.UrlPublica.TrimEnd('/');

    public string Montar(string caminho) => $"{_base}/{caminho.TrimStart('/')}";
}

/// <summary>Seção <c>Api</c>: endereço pelo qual navegadores e clientes de e-mail alcançam a API.</summary>
public sealed class ApiPublicaOptions
{
    public const string Section = "Api";

    public string UrlPublica { get; set; } = "http://localhost:5080";
}
