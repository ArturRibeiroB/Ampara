using FluentValidation;

namespace Funeraria.Modules.Planos.Application.Commands.AtualizarPlano;

internal sealed class AtualizarPlanoCommandValidator : AbstractValidator<AtualizarPlanoCommand>
{
    public AtualizarPlanoCommandValidator()
    {
        RuleFor(c => c.IdPlano).NotEmpty().WithMessage("Plano não informado.");

        PlanoDadosValidator.AplicarRegras(this);
    }
}
