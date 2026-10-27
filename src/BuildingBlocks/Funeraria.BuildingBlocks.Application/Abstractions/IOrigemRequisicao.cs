namespace Funeraria.BuildingBlocks.Application.Abstractions;

/// <summary>De onde veio a requisição atual: usado na auditoria (AUT-09) e no registro de aceite de termos (SEG-01).</summary>
public interface IOrigemRequisicao
{
    /// <summary>IP do cliente, já considerando o proxy reverso confiável (SEG-02). Nulo fora de uma requisição HTTP.</summary>
    string? Ip { get; }
}
