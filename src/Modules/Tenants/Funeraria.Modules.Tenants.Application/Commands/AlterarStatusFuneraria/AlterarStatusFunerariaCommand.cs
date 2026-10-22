using FluentValidation;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Tenants.Application.Abstractions;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Application.Commands.AlterarStatusFuneraria;

/// <summary>
/// ADMIN aprova, suspende ou reativa uma funerária (TEN-02). Enquanto o onboarding de pagamentos (DEC-09)
/// não existir, a ativação é manual.
/// </summary>
public sealed record AlterarStatusFunerariaCommand(Guid IdTenant, StatusFuneraria? Status) : ICommand;

internal sealed class AlterarStatusFunerariaCommandValidator : AbstractValidator<AlterarStatusFunerariaCommand>
{
    public AlterarStatusFunerariaCommandValidator()
    {
        RuleFor(c => c.Status).NotNull().IsInEnum().WithMessage("Status inválido. Use PENDENTE, ATIVA ou SUSPENSA.");
    }
}

public sealed class AlterarStatusFunerariaCommandHandler(
    IFunerariaRepository funerarias,
    ITenantsUnitOfWork unitOfWork,
    ITenantCache cache)
    : ICommandHandler<AlterarStatusFunerariaCommand>
{
    public async ValueTask<Result> Handle(AlterarStatusFunerariaCommand command, CancellationToken cancellationToken)
    {
        FunerariaEntity? funeraria = await funerarias.ObterPorIdAsync(command.IdTenant, cancellationToken);
        if (funeraria is null)
        {
            return FunerariaErros.NaoEncontrada;
        }

        Result result = funeraria.AlterarStatus(command.Status!.Value);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidar(funeraria.Id, funeraria.Slug);

        return Result.Success();
    }
}
