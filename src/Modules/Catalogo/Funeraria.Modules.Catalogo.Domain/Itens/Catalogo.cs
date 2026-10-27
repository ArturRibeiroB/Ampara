using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Catalogo.Domain.Itens;

/// <summary>Categoria do catálogo da funerária, ex.: Urna, Flores, Velório (FUN-01). Não é excluída, só desativada.</summary>
public sealed class Categoria : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private Categoria(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private Categoria()
    {
    }

    public Guid TenantId { get; private set; }

    public string Nome { get; private set; } = null!;

    /// <summary>Posição na lista mostrada ao cliente.</summary>
    public int Ordem { get; private set; }

    public bool Ativo { get; private set; }

    public static Categoria Criar(Guid tenantId, string nome, int ordem) =>
        new(Guid.CreateVersion7(), tenantId) { Nome = nome.Trim(), Ordem = ordem, Ativo = true };

    public void Atualizar(string nome, int ordem)
    {
        Nome = nome.Trim();
        Ordem = ordem;
    }

    public void AlterarStatus(bool ativo) => Ativo = ativo;
}

/// <summary>
/// Produto ou serviço do catálogo (FUN-01, DEC-07). Não é excluído, só desativado: funerais, orçamentos e coberturas
/// que já usam o item continuam apontando para ele. Quem usa o preço guarda o valor do momento (ex.: orçamento aprovado).
/// </summary>
public sealed class ItemCatalogo : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private readonly List<Variacao> _variacoes = [];

    private ItemCatalogo(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private ItemCatalogo()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdCategoria { get; private set; }

    public string Nome { get; private set; } = null!;

    public string? Descricao { get; private set; }

    public decimal Preco { get; private set; }

    public bool ControlaEstoque { get; private set; }

    /// <summary>Preenchido quando o item controla estoque.</summary>
    public int? QuantidadeEstoque { get; private set; }

    /// <summary>Chave da imagem no armazenamento de arquivos (INF-18).</summary>
    public string? ImagemChave { get; private set; }

    /// <summary>Endereço público da imagem, servido pela API. Muda a cada upload.</summary>
    public string? ImagemUrl { get; private set; }

    /// <summary>Terceiro FORNECEDOR que entrega o item: entrar num funeral gera um pedido para ele (FUN-09).</summary>
    public Guid? IdTerceiroFornecedor { get; private set; }

    public bool Ativo { get; private set; }

    public IReadOnlyList<Variacao> Variacoes => _variacoes;

    public static ItemCatalogo Criar(Guid tenantId, Guid idCategoria, DadosItem dados, bool ativo = true)
    {
        var item = new ItemCatalogo(Guid.CreateVersion7(), tenantId) { Ativo = ativo };
        item.Atualizar(idCategoria, dados);
        return item;
    }

    public void Atualizar(Guid idCategoria, DadosItem dados)
    {
        IdCategoria = idCategoria;
        Nome = dados.Nome.Trim();
        Descricao = string.IsNullOrWhiteSpace(dados.Descricao) ? null : dados.Descricao.Trim();
        Preco = dados.Preco;
        ControlaEstoque = dados.ControlaEstoque;
        QuantidadeEstoque = dados.ControlaEstoque ? dados.QuantidadeEstoque ?? 0 : null;
        IdTerceiroFornecedor = dados.IdTerceiroFornecedor;
    }

    public void AlterarStatus(bool ativo) => Ativo = ativo;

    /// <returns>Chave da imagem anterior, para remover do armazenamento.</returns>
    public string? DefinirImagem(string chave, string url)
    {
        string? anterior = ImagemChave;
        ImagemChave = chave;
        ImagemUrl = url;
        return anterior;
    }

    public Result<Variacao> AdicionarVariacao(string nome, decimal acrescimoPreco)
    {
        string normalizado = nome.Trim();
        if (_variacoes.Any(v => string.Equals(v.Nome, normalizado, StringComparison.OrdinalIgnoreCase)))
        {
            return CatalogoErros.VariacaoDuplicada;
        }

        if (Preco + acrescimoPreco < 0)
        {
            return CatalogoErros.VariacaoComPrecoNegativo;
        }

        var variacao = Variacao.Criar(normalizado, acrescimoPreco);
        _variacoes.Add(variacao);
        return variacao;
    }

    public Result AlterarStatusVariacao(Guid idVariacao, bool ativo)
    {
        Variacao? variacao = _variacoes.FirstOrDefault(v => v.Id == idVariacao);
        if (variacao is null)
        {
            return CatalogoErros.VariacaoNaoEncontrada;
        }

        variacao.AlterarStatus(ativo);
        return Result.Success();
    }
}

/// <summary>Versão do item com outro preço, ex.: urna em mogno, coroa de flores grande (FUN-01).</summary>
public sealed class Variacao : Entity<Guid>
{
    private Variacao(Guid id)
        : base(id)
    {
    }

    private Variacao()
    {
    }

    public string Nome { get; private set; } = null!;

    /// <summary>Diferença sobre o preço do item. Pode ser negativa (versão mais simples), sem deixar o preço abaixo de zero.</summary>
    public decimal AcrescimoPreco { get; private set; }

    public bool Ativo { get; private set; }

    internal static Variacao Criar(string nome, decimal acrescimoPreco) =>
        new(Guid.CreateVersion7()) { Nome = nome, AcrescimoPreco = acrescimoPreco, Ativo = true };

    internal void AlterarStatus(bool ativo) => Ativo = ativo;
}

public sealed record DadosItem(
    string Nome, string? Descricao, decimal Preco, bool ControlaEstoque, int? QuantidadeEstoque, Guid? IdTerceiroFornecedor = null);

public static class CatalogoErros
{
    public static readonly Error CategoriaNaoEncontrada =
        Error.NaoEncontrado("Catalogo.CategoriaNaoEncontrada", "Categoria não encontrada.");

    public static readonly Error CategoriaDuplicada =
        Error.Conflito("Catalogo.CategoriaDuplicada", "Já existe uma categoria com este nome.");

    public static readonly Error ItemNaoEncontrado =
        Error.NaoEncontrado("Catalogo.ItemNaoEncontrado", "Item do catálogo não encontrado.");

    public static readonly Error ItemDuplicado =
        Error.Conflito("Catalogo.ItemDuplicado", "Já existe um item com este nome nesta categoria.");

    public static readonly Error VariacaoDuplicada =
        Error.Conflito("Catalogo.VariacaoDuplicada", "Já existe uma variação com este nome neste item.");

    public static readonly Error VariacaoNaoEncontrada =
        Error.NaoEncontrado("Catalogo.VariacaoNaoEncontrada", "Variação não encontrada.");

    public static readonly Error VariacaoComPrecoNegativo =
        Error.Validacao("Catalogo.VariacaoComPrecoNegativo", "O desconto da variação não pode deixar o preço abaixo de zero.");

    public static readonly Error FornecedorInvalido =
        Error.Validacao("Catalogo.FornecedorInvalido", "O fornecedor precisa ser um terceiro do tipo FORNECEDOR, ativo nesta funerária.");

    public static readonly Error SemImagem =
        Error.NaoEncontrado("Catalogo.SemImagem", "Este item não tem imagem.");
}

public interface ICategoriaRepository
{
    Task<Categoria?> ObterAsync(Guid idCategoria, CancellationToken cancellationToken = default);

    Task<bool> ExisteNomeAsync(string nome, Guid? exceto, CancellationToken cancellationToken = default);

    Task<bool> ExisteAlgumaAsync(CancellationToken cancellationToken = default);

    void Adicionar(Categoria categoria);
}

public interface IItemCatalogoRepository
{
    Task<ItemCatalogo?> ObterAsync(Guid idItem, CancellationToken cancellationToken = default);

    Task<bool> ExisteNomeAsync(Guid idCategoria, string nome, Guid? exceto, CancellationToken cancellationToken = default);

    /// <summary>Busca sem o filtro de tenant da requisição, para a imagem pública (o tenant vem do endereço).</summary>
    Task<ItemCatalogo?> ObterDaFunerariaAsync(Guid idTenant, Guid idItem, CancellationToken cancellationToken = default);

    void Adicionar(ItemCatalogo item);
}
