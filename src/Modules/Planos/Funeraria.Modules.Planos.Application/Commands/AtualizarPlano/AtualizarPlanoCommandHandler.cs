using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Planos.Application.Abstractions;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.Application.Commands.AtualizarPlano;

public sealed class AtualizarPlanoCommandHandler(IPlanoRepository planos, IPlanosUnitOfWork unitOfWork)
    : ICommandHandler<AtualizarPlanoCommand>
{
    public async ValueTask<Result> Handle(AtualizarPlanoCommand command, CancellationToken cancellationToken)
    {
        Plano? plano = await planos.ObterPorIdAsync(command.IdPlano, cancellationToken);
        if (plano is null)
        {
            return PlanoErros.NaoEncontrado;
        }

        if (await planos.ExisteComNomeAsync(command.Nome.Trim(), exceto: plano.Id, cancellationToken))
        {
            return PlanoErros.NomeDuplicado;
        }

        plano.Atualizar(
            command.Nome,
            command.Descricao,
            new PrecosPlano(command.ValorMensal, command.ValorAnual, command.ValorDependenteMensal, command.ValorDependenteAnual),
            command.CarenciaDias,
            command.LimiteDependentes,
            command.PermiteSomenteDependente,
            command.Configuracoes);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
