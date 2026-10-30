namespace Funeraria.Modules.Catalogo.Contracts;

/// <summary>
/// Consulta ao catálogo pelos outros módulos: Planos (coberturas, PLA-02), Funeral (personalização e pedidos) e
/// Terceiros (FUN-01). Respeita o tenant da execução atual.
/// </summary>
public interface ICatalogoConsultas
{
    /// <summary>Itens com os ids informados, ativos ou não (quem chama decide se aceita item inativo). Ids sem item ficam de fora.</summary>
    Task<IReadOnlyList<ItemCatalogoDto>> ObterItensAsync(IReadOnlyCollection<Guid> idsItens, CancellationToken cancellationToken = default);

    /// <summary>Categorias com os ids informados (ex.: categorias atendidas por um fornecedor, TER-01). Ids sem categoria ficam de fora.</summary>
    Task<IReadOnlyList<CategoriaCatalogoDto>> ObterCategoriasAsync(IReadOnlyCollection<Guid> idsCategorias, CancellationToken cancellationToken = default);
}

/// <summary>
/// Estoque dos itens do catálogo, baixado quando o item entra num funeral (FUN-03). Item que não controla estoque
/// sempre aceita a baixa. Respeita o tenant da execução atual.
/// </summary>
public interface ICatalogoEstoque
{
    /// <summary>Baixa a quantidade numa operação só no banco (sem corrida). Falso se o estoque não for suficiente.</summary>
    Task<bool> BaixarAsync(Guid idItem, int quantidade, CancellationToken cancellationToken = default);

    /// <summary>Devolve ao estoque (item removido do funeral ou funeral cancelado).</summary>
    Task DevolverAsync(Guid idItem, int quantidade, CancellationToken cancellationToken = default);
}

public sealed record CategoriaCatalogoDto(Guid IdCategoria, string Nome, bool Ativo);

/// <param name="IdItem">Item do catálogo.</param>
/// <param name="IdCategoria">Categoria do item.</param>
/// <param name="Categoria">Nome da categoria.</param>
/// <param name="Nome">Nome do item.</param>
/// <param name="Preco">Preço atual. Quem guarda um valor (orçamento, cobertura) copia no momento do uso.</param>
/// <param name="Ativo">Item e categoria ativos.</param>
/// <param name="Variacoes">Variações ativas do item.</param>
/// <param name="IdTerceiroFornecedor">Fornecedor que recebe o pedido quando o item entra num funeral (FUN-09).</param>
/// <param name="ControlaEstoque">O item tem estoque, baixado quando entra num funeral (FUN-03).</param>
public sealed record ItemCatalogoDto(
    Guid IdItem,
    Guid IdCategoria,
    string Categoria,
    string Nome,
    decimal Preco,
    bool Ativo,
    IReadOnlyList<VariacaoDto> Variacoes,
    Guid? IdTerceiroFornecedor = null,
    bool ControlaEstoque = false);

public sealed record VariacaoDto(Guid IdVariacao, string Nome, decimal AcrescimoPreco);
