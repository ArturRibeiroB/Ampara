using FluentValidation;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.Modules.Clientes.Application.Validacao;

namespace Funeraria.Modules.Clientes.Application.Commands.Dependentes;

/// <summary>Dados editáveis do dependente, comuns ao cadastro e à edição.</summary>
public interface IDadosDependente
{
    string Nome { get; }

    DateOnly? DataNascimento { get; }

    string Parentesco { get; }

    string? Telefone { get; }

    string? Email { get; }
}

/// <summary>Validações com as mensagens do card Criar Dependente.</summary>
internal static class RegrasDependente
{
    public static void Aplicar<T>(AbstractValidator<T> validator, TimeProvider timeProvider)
        where T : IDadosDependente
    {
        validator.RuleFor(d => d.Nome)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MaximumLength(150).WithMessage("Nome deve ter no máximo 150 caracteres.");

        validator.RuleFor(d => d.DataNascimento)
            .Must(d => DataNascimento.Valida(d, timeProvider)).WithMessage("Data de nascimento inválida.");

        validator.RuleFor(d => d.Parentesco)
            .NotEmpty().WithMessage("Parentesco é obrigatório.")
            .MaximumLength(50).WithMessage("Parentesco deve ter no máximo 50 caracteres.");

        validator.RuleFor(d => d.Telefone)
            .Must(t => string.IsNullOrWhiteSpace(t) || RegrasComuns.TelefoneValido(t)).WithMessage("Telefone inválido.");

        validator.RuleFor(d => d.Email)
            .EmailAddress().WithMessage("E-mail inválido.")
            .MaximumLength(254).WithMessage("E-mail inválido.")
            .When(d => !string.IsNullOrWhiteSpace(d.Email));
    }
}
