using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Planos.Application.Abstractions;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.Application.Commands.CriarPlano;

public sealed class CriarPlanoCommandHandler(
    IPlanoRepository planos,
    IPlanosUnitOfWork unitOfWork,
    ITenantContext tenantContext)
    : ICommandHandler<CriarPlanoCommand, Guid>
{
    public async ValueTask<Result<Guid>> Handle(CriarPlanoCommand command, CancellationToken cancellationToken)
    {
        if (await planos.ExisteComNomeAsync(command.Nome.Trim(), cancellationToken: cancellationToken))
        {
            return PlanoErros.NomeDuplicado;
        }

        var plano = Plano.Criar(
            tenantContext.RequiredTenantId,
            command.Nome,
            command.Descricao,
            new PrecosPlano(command.ValorMensal, command.ValorAnual, command.ValorDependenteMensal, command.ValorDependenteAnual),
            command.CarenciaDias,
            command.LimiteDependentes,
            command.PermiteSomenteDependente,
            command.Configuracoes);

        planos.Adicionar(plano);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return plano.Id;
    }
}
