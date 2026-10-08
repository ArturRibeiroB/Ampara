using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Planos.Application.Abstractions;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.Application.Commands.AlterarStatusPlano;

public sealed class AlterarStatusPlanoCommandHandler(IPlanoRepository planos, IPlanosUnitOfWork unitOfWork)
    : ICommandHandler<AlterarStatusPlanoCommand>
{
    public async ValueTask<Result> Handle(AlterarStatusPlanoCommand command, CancellationToken cancellationToken)
    {
        Plano? plano = await planos.ObterPorIdAsync(command.IdPlano, cancellationToken);
        if (plano is null)
        {
            return PlanoErros.NaoEncontrado;
        }

        plano.AlterarStatus(command.Ativo);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
