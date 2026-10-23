using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Catalogo.Application.Abstractions;
using Funeraria.Modules.Catalogo.Domain.Itens;
using Funeraria.Modules.Tenants.Contracts;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Catalogo.Application.Itens;

public interface IDadosItem
{
    Guid? IdCategoria { get; }

    string Nome { get; }

    string? Descricao { get; }

    decimal? Preco { get; }

    bool ControlaEstoque { get; }

    int? QuantidadeEstoque { get; }
}

public sealed record CriarItemCommand(
    Guid? IdCategoria, string Nome, string? Descricao, decimal? Preco, bool ControlaEstoque, int? QuantidadeEstoque)
    : ICommand<ItemResponse>, IDadosItem;

/// <summary>Alterar o preço não muda funerais e orçamentos já aprovados: eles guardam o valor do momento (FUN-01).</summary>
public sealed record AtualizarItemCommand(
    Guid IdItem, Guid? IdCategoria, string Nome, string? Descricao, decimal? Preco, bool ControlaEstoque, int? QuantidadeEstoque)
    : ICommand<ItemResponse>, IDadosItem;

/// <summary>Item não é excluído, só desativado (FUN-01).</summary>
public sealed record AlterarStatusItemCommand(Guid IdItem, bool? Ativo) : ICommand;

public sealed record AdicionarVariacaoCommand(Guid IdItem, string Nome, decimal? AcrescimoPreco) : ICommand<ItemResponse>;

public sealed record AlterarStatusVariacaoCommand(Guid IdItem, Guid IdVariacao, bool? Ativo) : ICommand;

/// <summary>Imagem do item (INF-18): PNG, JPEG ou WEBP de até 5 MB.</summary>
public sealed record EnviarImagemItemCommand(Guid IdItem, ArquivoEnviado? Arquivo) : ICommand<ItemResponse>;

/// <summary>Catálogo: para o cliente, só itens ativos de categorias ativas; para o gestor, tudo.</summary>
public sealed record ListarItensQuery(Guid? IdCategoria, bool SomenteAtivos) : IQuery<IReadOnlyList<ItemResponse>>;

public sealed record ObterItemQuery(Guid IdItem, bool SomenteAtivo) : IQuery<ItemResponse>;

/// <summary>Imagem pública do item, para telas e e-mails. O armazenamento continua privado.</summary>
public sealed record ObterImagemItemQuery(string Slug, Guid IdItem) : IQuery<ArquivoLido>;

public sealed record ItemResponse(
    Guid IdItem,
    Guid IdCategoria,
    string Nome,
    string? Descricao,
    decimal Preco,
    bool ControlaEstoque,
    int? QuantidadeEstoque,
    string? ImagemUrl,
    bool Ativo,
    IReadOnlyList<VariacaoResponse> Variacoes)
{
    /// <summary>Para o gestor: tudo. Para o cliente: sem estoque e só as variações ativas.</summary>
    public static ItemResponse De(ItemCatalogo i, bool publico = false) => new(
        i.Id,
        i.IdCategoria,
        i.Nome,
        i.Descricao,
        i.Preco,
        i.ControlaEstoque,
        publico ? null : i.QuantidadeEstoque,
        i.ImagemUrl,
        i.Ativo,
        [.. i.Variacoes.Where(v => !publico || v.Ativo).Select(v => new VariacaoResponse(v.Id, v.Nome, v.AcrescimoPreco, v.Ativo))]);
}

public sealed record VariacaoResponse(Guid IdVariacao, string Nome, decimal AcrescimoPreco, bool Ativo);

internal abstract class DadosItemValidator<T> : AbstractValidator<T>
    where T : IDadosItem
{
    protected DadosItemValidator()
    {
        RuleFor(i => i.IdCategoria).NotEmpty().WithMessage("Categoria é obrigatória.");
        RuleFor(i => i.Nome)
            .NotEmpty().WithMessage("Nome do item é obrigatório.")
            .MaximumLength(150).WithMessage("Nome do item deve ter no máximo 150 caracteres.");
        RuleFor(i => i.Descricao).MaximumLength(1000).WithMessage("Descrição deve ter no máximo 1000 caracteres.");
        RuleFor(i => i.Preco)
            .NotNull().WithMessage("Preço é obrigatório.")
            .GreaterThanOrEqualTo(0).WithMessage("Preço não pode ser negativo.");
        RuleFor(i => i.QuantidadeEstoque)
            .NotNull().WithMessage("Informe a quantidade em estoque.")
            .GreaterThanOrEqualTo(0).WithMessage("Quantidade em estoque não pode ser negativa.")
            .When(i => i.ControlaEstoque);
    }
}

internal sealed class CriarItemCommandValidator : DadosItemValidator<CriarItemCommand>;

internal sealed class AtualizarItemCommandValidator : DadosItemValidator<AtualizarItemCommand>;

internal sealed class AlterarStatusItemCommandValidator : AbstractValidator<AlterarStatusItemCommand>
{
    public AlterarStatusItemCommandValidator() =>
        RuleFor(c => c.Ativo).NotNull().WithMessage("Informe se o item fica ativo (true) ou não (false).");
}

internal sealed class AlterarStatusVariacaoCommandValidator : AbstractValidator<AlterarStatusVariacaoCommand>
{
    public AlterarStatusVariacaoCommandValidator() =>
        RuleFor(c => c.Ativo).NotNull().WithMessage("Informe se a variação fica ativa (true) ou não (false).");
}

internal sealed class AdicionarVariacaoCommandValidator : AbstractValidator<AdicionarVariacaoCommand>
{
    public AdicionarVariacaoCommandValidator()
    {
        RuleFor(v => v.Nome)
            .NotEmpty().WithMessage("Nome da variação é obrigatório.")
            .MaximumLength(100).WithMessage("Nome da variação deve ter no máximo 100 caracteres.");
        RuleFor(v => v.AcrescimoPreco).NotNull().WithMessage("Acréscimo de preço é obrigatório (use 0 se não houver).");
    }
}

internal sealed class EnviarImagemItemCommandValidator : AbstractValidator<EnviarImagemItemCommand>
{
    public EnviarImagemItemCommandValidator() =>
        RuleFor(c => c.Arquivo).NotNull().WithName(ValidadorArquivo.Campo).WithMessage("Envie o arquivo da imagem.");
}

public sealed class CriarItemCommandHandler(
    IItemCatalogoRepository itens,
    ICategoriaRepository categorias,
    ICatalogoUnitOfWork unitOfWork,
    ITenantContext tenantContext)
    : ICommandHandler<CriarItemCommand, ItemResponse>
{
    public async ValueTask<Result<ItemResponse>> Handle(CriarItemCommand command, CancellationToken cancellationToken)
    {
        Result conferido = await ItensComuns.ConferirAsync(categorias, itens, command, exceto: null, cancellationToken);
        if (conferido.IsFailure)
        {
            return Result.Failure<ItemResponse>(conferido.Error);
        }

        var item = ItemCatalogo.Criar(tenantContext.RequiredTenantId, command.IdCategoria!.Value, ItensComuns.Dados(command));
        itens.Adicionar(item);
        return await ItensComuns.SalvarAsync(unitOfWork, item, cancellationToken);
    }
}

public sealed class AtualizarItemCommandHandler(
    IItemCatalogoRepository itens,
    ICategoriaRepository categorias,
    ICatalogoUnitOfWork unitOfWork)
    : ICommandHandler<AtualizarItemCommand, ItemResponse>
{
    public async ValueTask<Result<ItemResponse>> Handle(AtualizarItemCommand command, CancellationToken cancellationToken)
    {
        ItemCatalogo? item = await itens.ObterAsync(command.IdItem, cancellationToken);
        if (item is null)
        {
            return CatalogoErros.ItemNaoEncontrado;
        }

        Result conferido = await ItensComuns.ConferirAsync(categorias, itens, command, exceto: item.Id, cancellationToken);
        if (conferido.IsFailure)
        {
            return Result.Failure<ItemResponse>(conferido.Error);
        }

        item.Atualizar(command.IdCategoria!.Value, ItensComuns.Dados(command));
        return await ItensComuns.SalvarAsync(unitOfWork, item, cancellationToken);
    }
}

public sealed class AlterarStatusItemCommandHandler(IItemCatalogoRepository itens, ICatalogoUnitOfWork unitOfWork)
    : ICommandHandler<AlterarStatusItemCommand>
{
    public async ValueTask<Result> Handle(AlterarStatusItemCommand command, CancellationToken cancellationToken)
    {
        ItemCatalogo? item = await itens.ObterAsync(command.IdItem, cancellationToken);
        if (item is null)
        {
            return CatalogoErros.ItemNaoEncontrado;
        }

        item.AlterarStatus(command.Ativo!.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class AdicionarVariacaoCommandHandler(IItemCatalogoRepository itens, ICatalogoUnitOfWork unitOfWork)
    : ICommandHandler<AdicionarVariacaoCommand, ItemResponse>
{
    public async ValueTask<Result<ItemResponse>> Handle(AdicionarVariacaoCommand command, CancellationToken cancellationToken)
    {
        ItemCatalogo? item = await itens.ObterAsync(command.IdItem, cancellationToken);
        if (item is null)
        {
            return CatalogoErros.ItemNaoEncontrado;
        }

        Result<Variacao> variacao = item.AdicionarVariacao(command.Nome, command.AcrescimoPreco!.Value);
        if (variacao.IsFailure)
        {
            return Result.Failure<ItemResponse>(variacao.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ItemResponse.De(item);
    }
}

public sealed class AlterarStatusVariacaoCommandHandler(IItemCatalogoRepository itens, ICatalogoUnitOfWork unitOfWork)
    : ICommandHandler<AlterarStatusVariacaoCommand>
{
    public async ValueTask<Result> Handle(AlterarStatusVariacaoCommand command, CancellationToken cancellationToken)
    {
        ItemCatalogo? item = await itens.ObterAsync(command.IdItem, cancellationToken);
        if (item is null)
        {
            return CatalogoErros.ItemNaoEncontrado;
        }

        Result alterada = item.AlterarStatusVariacao(command.IdVariacao, command.Ativo!.Value);
        if (alterada.IsFailure)
        {
            return alterada;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class EnviarImagemItemCommandHandler(
    IItemCatalogoRepository itens,
    ICatalogoUnitOfWork unitOfWork,
    IArmazenamentoArquivos armazenamento,
    ITenantsApi tenants,
    IUrlPublicaApi urlPublica,
    ILogger<EnviarImagemItemCommandHandler> logger)
    : ICommandHandler<EnviarImagemItemCommand, ItemResponse>
{
    public async ValueTask<Result<ItemResponse>> Handle(EnviarImagemItemCommand command, CancellationToken cancellationToken)
    {
        ItemCatalogo? item = await itens.ObterAsync(command.IdItem, cancellationToken);
        FunerariaResumoDto? funeraria = item is null ? null : await tenants.ObterPorIdAsync(item.TenantId, cancellationToken);
        if (item is null || funeraria is null)
        {
            return CatalogoErros.ItemNaoEncontrado;
        }

        Result<ArquivoSalvo> salvo = await armazenamento.SalvarAsync(item.TenantId, "catalogo", command.Arquivo!, PoliticaArquivo.Imagem, cancellationToken);
        if (salvo.IsFailure)
        {
            return Result.Failure<ItemResponse>(salvo.Error);
        }

        string url = urlPublica.Montar(
            $"/api/v1/funerarias/{Uri.EscapeDataString(funeraria.Slug)}/catalogo/{item.Id}/imagem?v={salvo.Value.Hash[..12]}");
        string? anterior = item.DefinirImagem(salvo.Value.Chave, url);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (anterior is not null)
        {
            try
            {
                await armazenamento.RemoverAsync(anterior, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Não foi possível remover a imagem antiga {Chave} do armazenamento.", anterior);
            }
        }

        return ItemResponse.De(item);
    }
}

public sealed class ListarItensQueryHandler(ICatalogoLeitura leitura) : IQueryHandler<ListarItensQuery, IReadOnlyList<ItemResponse>>
{
    public async ValueTask<Result<IReadOnlyList<ItemResponse>>> Handle(ListarItensQuery query, CancellationToken cancellationToken) =>
        (await leitura.ListarItensAsync(query.IdCategoria, query.SomenteAtivos, cancellationToken))
            .Select(i => ItemResponse.De(i, publico: query.SomenteAtivos))
            .ToList();
}

public sealed class ObterItemQueryHandler(IItemCatalogoRepository itens, ICatalogoLeitura leitura) : IQueryHandler<ObterItemQuery, ItemResponse>
{
    public async ValueTask<Result<ItemResponse>> Handle(ObterItemQuery query, CancellationToken cancellationToken)
    {
        ItemCatalogo? item = await itens.ObterAsync(query.IdItem, cancellationToken);
        bool visivel = item is not null && (!query.SomenteAtivo || (item.Ativo && await leitura.CategoriaAtivaAsync(item.IdCategoria, cancellationToken)));
        return visivel ? ItemResponse.De(item!, publico: query.SomenteAtivo) : CatalogoErros.ItemNaoEncontrado;
    }
}

public sealed class ObterImagemItemQueryHandler(IItemCatalogoRepository itens, ITenantsApi tenants, IArmazenamentoArquivos armazenamento)
    : IQueryHandler<ObterImagemItemQuery, ArquivoLido>
{
    public async ValueTask<Result<ArquivoLido>> Handle(ObterImagemItemQuery query, CancellationToken cancellationToken)
    {
        FunerariaResumoDto? funeraria = await tenants.ObterPorSlugAsync(query.Slug, cancellationToken);
        ItemCatalogo? item = funeraria is null ? null : await itens.ObterDaFunerariaAsync(funeraria.IdTenant, query.IdItem, cancellationToken);
        if (item?.ImagemChave is null)
        {
            return CatalogoErros.SemImagem;
        }

        ArquivoLido? imagem = await armazenamento.AbrirAsync(item.ImagemChave, cancellationToken);
        return imagem is null ? CatalogoErros.SemImagem : imagem;
    }
}

internal static class ItensComuns
{
    public static DadosItem Dados(IDadosItem i) => new(i.Nome, i.Descricao, i.Preco!.Value, i.ControlaEstoque, i.QuantidadeEstoque);

    public static async Task<Result> ConferirAsync(
        ICategoriaRepository categorias, IItemCatalogoRepository itens, IDadosItem dados, Guid? exceto, CancellationToken cancellationToken)
    {
        if (await categorias.ObterAsync(dados.IdCategoria!.Value, cancellationToken) is null)
        {
            return CatalogoErros.CategoriaNaoEncontrada;
        }

        return await itens.ExisteNomeAsync(dados.IdCategoria.Value, dados.Nome.Trim(), exceto, cancellationToken)
            ? CatalogoErros.ItemDuplicado
            : Result.Success();
    }

    public static async Task<Result<ItemResponse>> SalvarAsync(ICatalogoUnitOfWork unitOfWork, ItemCatalogo item, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException)
        {
            return CatalogoErros.ItemDuplicado;
        }

        return ItemResponse.De(item);
    }
}
