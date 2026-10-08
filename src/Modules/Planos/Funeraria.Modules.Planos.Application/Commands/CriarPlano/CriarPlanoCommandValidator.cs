using FluentValidation;

namespace Funeraria.Modules.Planos.Application.Commands.CriarPlano;

internal sealed class CriarPlanoCommandValidator : AbstractValidator<CriarPlanoCommand>
{
    public CriarPlanoCommandValidator()
    {
        PlanoDadosValidator.AplicarRegras(this);
    }
}
