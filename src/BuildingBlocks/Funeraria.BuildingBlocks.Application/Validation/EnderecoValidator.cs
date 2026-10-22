using FluentValidation;
using Funeraria.BuildingBlocks.Domain.ValueObjects;

namespace Funeraria.BuildingBlocks.Application.Validation;

/// <summary>UF válida, CEP com 8 dígitos e tamanhos máximos (INF-06).</summary>
public sealed class EnderecoValidator : AbstractValidator<Endereco>
{
    public EnderecoValidator()
    {
        RuleFor(e => e.Logradouro).MaximumLength(200).WithMessage("Logradouro deve ter no máximo 200 caracteres.");
        RuleFor(e => e.Numero).MaximumLength(20).WithMessage("Número deve ter no máximo 20 caracteres.");
        RuleFor(e => e.Complemento).MaximumLength(100).WithMessage("Complemento deve ter no máximo 100 caracteres.");
        RuleFor(e => e.Bairro).MaximumLength(100).WithMessage("Bairro deve ter no máximo 100 caracteres.");
        RuleFor(e => e.Cidade).MaximumLength(100).WithMessage("Cidade deve ter no máximo 100 caracteres.");
        RuleFor(e => e.Uf).Must(RegrasComuns.UfValida).WithMessage("UF inválida.");
        RuleFor(e => e.Cep).Must(RegrasComuns.CepValido).WithMessage("CEP inválido.");
    }
}
