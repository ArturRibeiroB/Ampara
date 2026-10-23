using FluentValidation;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Catalogo.Contracts;
using Funeraria.Modules.Planos.Application.Abstractions;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.Application.Commands.DefinirCoberturas;

/// <summary>
/// O gestor define o que o plano cobre, trocando a lista inteira (PLA-02). Cada item precisa existir e estar ativo
/// no catálogo da funerária (FUN-01). Lista vazia remove as coberturas.
/// </summary>
public sealed record DefinirCoberturasCommand(Guid IdPlano, IReadOnlyList<CoberturaRequest>? Coberturas) : ICommand;

/// <param name="IdItemCatalogo">Item do catálogo coberto.</param>
/// <param name="Quantidade">Quantidade coberta (ex.: 1 coroa de flores).</param>
/// <param name="KmTransladoIncluso">Quilômetros de translado incluídos, quando o item é de translado.</param>
public sealed record CoberturaRequest(Guid? IdItemCatalogo, int? Quantidade, int? KmTransladoIncluso);

internal sealed class DefinirCoberturasCommandValidator : AbstractValidator<DefinirCoberturasCommand>
{
    public DefinirCoberturasCommandValidator()
    {
        RuleFor(c => c.Coberturas).NotNull().WithMessage("Informe a lista de coberturas (vazia remove todas).");

        RuleFor(c => c.Coberturas)
            .Must(l => l!.Where(c => c.IdItemCatalogo is not null).GroupBy(c => c.IdItemCatalogo).All(g => g.Count() == 1))
            .WithMessage("Item repetido na lista de coberturas: informe cada item uma vez, com a quantidade.")
            .When(c => c.Coberturas is not null);

        RuleForEach(c => c.Coberturas).ChildRules(cobertura =>
        {
            cobertura.RuleFor(c => c.IdItemCatalogo).NotEmpty().WithMessage("Item do catálogo é obrigatório.");
            cobertura.RuleFor(c => c.Quantidade)
                .NotNull().WithMessage("Quantidade é obrigatória.")
                .GreaterThanOrEqualTo(1).WithMessage("Quantidade deve ser pelo menos 1.");
            cobertura.RuleFor(c => c.KmTransladoIncluso).GreaterThanOrEqualTo(0).WithMessage("Km de translado não pode ser negativo.");
        });
    }
}

public sealed class DefinirCoberturasCommandHandler(
    IPlanoRepository planos,
    IPlanosUnitOfWork unitOfWork,
    ICatalogoConsultas catalogo)
    : ICommandHandler<DefinirCoberturasCommand>
{
    public async ValueTask<Result> Handle(DefinirCoberturasCommand command, CancellationToken cancellationToken)
    {
        Plano? plano = await planos.ObterPorIdAsync(command.IdPlano, cancellationToken);
        if (plano is null)
        {
            return PlanoErros.NaoEncontrado;
        }

        Guid[] ids = [.. command.Coberturas!.Select(c => c.IdItemCatalogo!.Value)];
        HashSet<Guid> ativos = [.. (await catalogo.ObterItensAsync(ids, cancellationToken)).Where(i => i.Ativo).Select(i => i.IdItem)];
        Guid[] invalidos = [.. ids.Where(id => !ativos.Contains(id))];
        if (invalidos.Length > 0)
        {
            return PlanoErros.ItensDeCoberturaInvalidos(invalidos);
        }

        plano.DefinirCoberturas(command.Coberturas!.Select(c => new CoberturaDefinida(c.IdItemCatalogo!.Value, c.Quantidade!.Value, c.KmTransladoIncluso)));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
