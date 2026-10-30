using Funeraria.Modules.Catalogo.Application.Abstractions;
using Funeraria.Modules.Catalogo.Contracts;
using Funeraria.Modules.Catalogo.Domain.Itens;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Catalogo.Infrastructure.Database;

internal sealed class CategoriaRepository(CatalogoDbContext context) : ICategoriaRepository
{
    public Task<Categoria?> ObterAsync(Guid idCategoria, CancellationToken cancellationToken = default) =>
        context.Categorias.FirstOrDefaultAsync(c => c.Id == idCategoria, cancellationToken);

    public Task<bool> ExisteNomeAsync(string nome, Guid? exceto, CancellationToken cancellationToken = default) =>
        context.Categorias.AnyAsync(c => c.Nome.ToLower() == nome.ToLower() && c.Id != exceto, cancellationToken);

    public Task<bool> ExisteAlgumaAsync(CancellationToken cancellationToken = default) => context.Categorias.AnyAsync(cancellationToken);

    public void Adicionar(Categoria categoria) => context.Categorias.Add(categoria);
}

internal sealed class ItemCatalogoRepository(CatalogoDbContext context) : IItemCatalogoRepository
{
    public Task<ItemCatalogo?> ObterAsync(Guid idItem, CancellationToken cancellationToken = default) =>
        context.Itens.FirstOrDefaultAsync(i => i.Id == idItem, cancellationToken);

    public Task<bool> ExisteNomeAsync(Guid idCategoria, string nome, Guid? exceto, CancellationToken cancellationToken = default) =>
        context.Itens.AnyAsync(i => i.IdCategoria == idCategoria && i.Nome.ToLower() == nome.ToLower() && i.Id != exceto, cancellationToken);

    // A imagem pública não tem tenant na requisição: a funerária vem do endereço e o filtro é feito aqui.
    public Task<ItemCatalogo?> ObterDaFunerariaAsync(Guid idTenant, Guid idItem, CancellationToken cancellationToken = default) =>
        context.Itens.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(i => i.TenantId == idTenant && i.Id == idItem, cancellationToken);

    public void Adicionar(ItemCatalogo item) => context.Itens.Add(item);
}

internal sealed class CatalogoLeitura(CatalogoDbContext context) : ICatalogoLeitura, ICatalogoConsultas, ICatalogoEstoque
{
    public async Task<IReadOnlyList<Categoria>> ListarCategoriasAsync(bool somenteAtivas, CancellationToken cancellationToken = default) =>
        await context.Categorias
            .AsNoTracking()
            .Where(c => !somenteAtivas || c.Ativo)
            .OrderBy(c => c.Ordem).ThenBy(c => c.Nome)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ItemCatalogo>> ListarItensAsync(Guid? idCategoria, bool somenteAtivos, CancellationToken cancellationToken = default) =>
        await context.Itens
            .AsNoTracking()
            .Where(i => idCategoria == null || i.IdCategoria == idCategoria)
            .Where(i => !somenteAtivos || (i.Ativo && context.Categorias.Any(c => c.Id == i.IdCategoria && c.Ativo)))
            .OrderBy(i => i.Nome)
            .ToListAsync(cancellationToken);

    public Task<bool> CategoriaAtivaAsync(Guid idCategoria, CancellationToken cancellationToken = default) =>
        context.Categorias.AnyAsync(c => c.Id == idCategoria && c.Ativo, cancellationToken);

    public async Task<IReadOnlyList<CategoriaCatalogoDto>> ObterCategoriasAsync(
        IReadOnlyCollection<Guid> idsCategorias,
        CancellationToken cancellationToken = default) =>
        idsCategorias.Count == 0
            ? []
            : await context.Categorias
                .AsNoTracking()
                .Where(c => idsCategorias.Contains(c.Id))
                .Select(c => new CategoriaCatalogoDto(c.Id, c.Nome, c.Ativo))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ItemCatalogoDto>> ObterItensAsync(
        IReadOnlyCollection<Guid> idsItens,
        CancellationToken cancellationToken = default)
    {
        if (idsItens.Count == 0)
        {
            return [];
        }

        List<ItemCatalogo> itens = await context.Itens.AsNoTracking().Where(i => idsItens.Contains(i.Id)).ToListAsync(cancellationToken);
        Guid[] idsCategorias = [.. itens.Select(i => i.IdCategoria).Distinct()];
        Dictionary<Guid, Categoria> categorias = await context.Categorias
            .AsNoTracking()
            .Where(c => idsCategorias.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        return itens
            .Select(i => new ItemCatalogoDto(
                i.Id,
                i.IdCategoria,
                categorias[i.IdCategoria].Nome,
                i.Nome,
                i.Preco,
                i.Ativo && categorias[i.IdCategoria].Ativo,
                [.. i.Variacoes.Where(v => v.Ativo).Select(v => new VariacaoDto(v.Id, v.Nome, v.AcrescimoPreco))],
                i.IdTerceiroFornecedor,
                i.ControlaEstoque))
            .ToList();
    }

    // UPDATE ... WHERE quantidade_estoque >= @quantidade: duas baixas ao mesmo tempo não deixam o estoque negativo.
    public async Task<bool> BaixarAsync(Guid idItem, int quantidade, CancellationToken cancellationToken = default)
    {
        if (!await context.Itens.AnyAsync(i => i.Id == idItem && i.ControlaEstoque, cancellationToken))
        {
            return true;
        }

        return await context.Itens
            .Where(i => i.Id == idItem && i.QuantidadeEstoque >= quantidade)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.QuantidadeEstoque, i => i.QuantidadeEstoque - quantidade), cancellationToken) == 1;
    }

    public Task DevolverAsync(Guid idItem, int quantidade, CancellationToken cancellationToken = default) =>
        context.Itens
            .Where(i => i.Id == idItem && i.ControlaEstoque)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.QuantidadeEstoque, i => i.QuantidadeEstoque + quantidade), cancellationToken);
}
