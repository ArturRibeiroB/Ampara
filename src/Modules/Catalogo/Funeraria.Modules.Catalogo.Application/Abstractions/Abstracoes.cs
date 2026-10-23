using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.Modules.Catalogo.Domain.Itens;

namespace Funeraria.Modules.Catalogo.Application.Abstractions;

public interface ICatalogoUnitOfWork : IUnitOfWork;

/// <summary>Leituras de listagem do catálogo, já com o filtro de tenant.</summary>
public interface ICatalogoLeitura
{
    Task<IReadOnlyList<Categoria>> ListarCategoriasAsync(bool somenteAtivas, CancellationToken cancellationToken = default);

    /// <summary>Com <paramref name="somenteAtivos"/>, só itens ativos de categorias ativas (o que o cliente vê).</summary>
    Task<IReadOnlyList<ItemCatalogo>> ListarItensAsync(Guid? idCategoria, bool somenteAtivos, CancellationToken cancellationToken = default);

    Task<bool> CategoriaAtivaAsync(Guid idCategoria, CancellationToken cancellationToken = default);
}
