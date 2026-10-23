namespace Funeraria.Modules.Catalogo.Contracts;

/// <summary>
/// Consulta ao catálogo pelos outros módulos: Planos (coberturas, PLA-02), Funeral (personalização e pedidos) e
/// Terceiros (FUN-01). Respeita o tenant da execução atual.
/// </summary>
public interface ICatalogoConsultas
{
    /// <summary>Itens com os ids informados, ativos ou não (quem chama decide se aceita item inativo). Ids sem item ficam de fora.</summary>
    Task<IReadOnlyList<ItemCatalogoDto>> ObterItensAsync(IReadOnlyCollection<Guid> idsItens, CancellationToken cancellationToken = default);
}

/// <param name="IdItem">Item do catálogo.</param>
/// <param name="IdCategoria">Categoria do item.</param>
/// <param name="Categoria">Nome da categoria.</param>
/// <param name="Nome">Nome do item.</param>
/// <param name="Preco">Preço atual. Quem guarda um valor (orçamento, cobertura) copia no momento do uso.</param>
/// <param name="Ativo">Item e categoria ativos.</param>
/// <param name="Variacoes">Variações ativas do item.</param>
public sealed record ItemCatalogoDto(
    Guid IdItem,
    Guid IdCategoria,
    string Categoria,
    string Nome,
    decimal Preco,
    bool Ativo,
    IReadOnlyList<VariacaoDto> Variacoes);

public sealed record VariacaoDto(Guid IdVariacao, string Nome, decimal AcrescimoPreco);
