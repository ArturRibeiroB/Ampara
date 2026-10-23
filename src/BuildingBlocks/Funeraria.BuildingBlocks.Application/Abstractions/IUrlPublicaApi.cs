namespace Funeraria.BuildingBlocks.Application.Abstractions;

/// <summary>Endereço pelo qual navegadores e clientes de e-mail alcançam a API (configuração <c>Api:UrlPublica</c>).</summary>
public interface IUrlPublicaApi
{
    /// <summary>URL absoluta para um caminho da API, ex.: <c>/api/v1/funerarias/demo/logo</c>.</summary>
    string Montar(string caminho);
}
