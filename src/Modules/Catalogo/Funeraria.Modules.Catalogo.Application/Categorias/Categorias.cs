using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Catalogo.Application.Abstractions;
using Funeraria.Modules.Catalogo.Domain.Itens;

namespace Funeraria.Modules.Catalogo.Application.Categorias;

public interface IDadosCategoria
{
    string Nome { get; }

    int? Ordem { get; }
}

public sealed record CriarCategoriaCommand(string Nome, int? Ordem) : ICommand<CategoriaResponse>, IDadosCategoria;

public sealed record AtualizarCategoriaCommand(Guid IdCategoria, string Nome, int? Ordem) : ICommand<CategoriaResponse>, IDadosCategoria;

/// <summary>Categoria não é excluída, só desativada (FUN-01). Itens de categoria inativa somem do catálogo do cliente.</summary>
public sealed record AlterarStatusCategoriaCommand(Guid IdCategoria, bool? Ativo) : ICommand;

/// <summary>Categorias do catálogo: só as ativas para o cliente; todas para o gestor.</summary>
public sealed record ListarCategoriasQuery(bool SomenteAtivas) : IQuery<IReadOnlyList<CategoriaResponse>>;

public sealed record CategoriaResponse(Guid IdCategoria, string Nome, int Ordem, bool Ativo)
{
    public static CategoriaResponse De(Categoria c) => new(c.Id, c.Nome, c.Ordem, c.Ativo);
}

internal abstract class DadosCategoriaValidator<T> : AbstractValidator<T>
    where T : IDadosCategoria
{
    protected DadosCategoriaValidator()
    {
        RuleFor(c => c.Nome)
            .NotEmpty().WithMessage("Nome da categoria é obrigatório.")
            .MaximumLength(100).WithMessage("Nome da categoria deve ter no máximo 100 caracteres.");
        RuleFor(c => c.Ordem).GreaterThanOrEqualTo(0).WithMessage("Ordem deve ser zero ou maior.");
    }
}

internal sealed class CriarCategoriaCommandValidator : DadosCategoriaValidator<CriarCategoriaCommand>;

internal sealed class AtualizarCategoriaCommandValidator : DadosCategoriaValidator<AtualizarCategoriaCommand>;

internal sealed class AlterarStatusCategoriaCommandValidator : AbstractValidator<AlterarStatusCategoriaCommand>
{
    public AlterarStatusCategoriaCommandValidator() =>
        RuleFor(c => c.Ativo).NotNull().WithMessage("Informe se a categoria fica ativa (true) ou não (false).");
}

public sealed class CriarCategoriaCommandHandler(
    ICategoriaRepository categorias,
    ICatalogoUnitOfWork unitOfWork,
    ITenantContext tenantContext)
    : ICommandHandler<CriarCategoriaCommand, CategoriaResponse>
{
    public async ValueTask<Result<CategoriaResponse>> Handle(CriarCategoriaCommand command, CancellationToken cancellationToken)
    {
        if (await categorias.ExisteNomeAsync(command.Nome.Trim(), exceto: null, cancellationToken))
        {
            return CatalogoErros.CategoriaDuplicada;
        }

        var categoria = Categoria.Criar(tenantContext.RequiredTenantId, command.Nome, command.Ordem ?? 0);
        categorias.Adicionar(categoria);
        return await SalvarAsync(unitOfWork, categoria, cancellationToken);
    }

    internal static async Task<Result<CategoriaResponse>> SalvarAsync(ICatalogoUnitOfWork unitOfWork, Categoria categoria, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException)
        {
            return CatalogoErros.CategoriaDuplicada;
        }

        return CategoriaResponse.De(categoria);
    }
}

public sealed class AtualizarCategoriaCommandHandler(ICategoriaRepository categorias, ICatalogoUnitOfWork unitOfWork)
    : ICommandHandler<AtualizarCategoriaCommand, CategoriaResponse>
{
    public async ValueTask<Result<CategoriaResponse>> Handle(AtualizarCategoriaCommand command, CancellationToken cancellationToken)
    {
        Categoria? categoria = await categorias.ObterAsync(command.IdCategoria, cancellationToken);
        if (categoria is null)
        {
            return CatalogoErros.CategoriaNaoEncontrada;
        }

        if (await categorias.ExisteNomeAsync(command.Nome.Trim(), exceto: categoria.Id, cancellationToken))
        {
            return CatalogoErros.CategoriaDuplicada;
        }

        categoria.Atualizar(command.Nome, command.Ordem ?? categoria.Ordem);
        return await CriarCategoriaCommandHandler.SalvarAsync(unitOfWork, categoria, cancellationToken);
    }
}

public sealed class AlterarStatusCategoriaCommandHandler(ICategoriaRepository categorias, ICatalogoUnitOfWork unitOfWork)
    : ICommandHandler<AlterarStatusCategoriaCommand>
{
    public async ValueTask<Result> Handle(AlterarStatusCategoriaCommand command, CancellationToken cancellationToken)
    {
        Categoria? categoria = await categorias.ObterAsync(command.IdCategoria, cancellationToken);
        if (categoria is null)
        {
            return CatalogoErros.CategoriaNaoEncontrada;
        }

        categoria.AlterarStatus(command.Ativo!.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ListarCategoriasQueryHandler(ICatalogoLeitura leitura)
    : IQueryHandler<ListarCategoriasQuery, IReadOnlyList<CategoriaResponse>>
{
    public async ValueTask<Result<IReadOnlyList<CategoriaResponse>>> Handle(ListarCategoriasQuery query, CancellationToken cancellationToken) =>
        (await leitura.ListarCategoriasAsync(query.SomenteAtivas, cancellationToken)).Select(CategoriaResponse.De).ToList();
}
