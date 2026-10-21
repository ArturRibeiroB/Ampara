using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Tenants.Application.Abstractions;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Application.Commands.AtualizarConfiguracoes;

/// <summary>O gestor atualiza a identidade visual e os parâmetros da própria funerária (TEN-03).</summary>
public sealed record AtualizarConfiguracoesCommand(IdentidadeVisual? IdentidadeVisual, ParametrosFuneraria? Parametros) : ICommand;

public sealed class AtualizarConfiguracoesCommandHandler(
    IFunerariaRepository funerarias,
    ITenantsUnitOfWork unitOfWork,
    ITenantContext tenantContext)
    : ICommandHandler<AtualizarConfiguracoesCommand>
{
    public async ValueTask<Result> Handle(AtualizarConfiguracoesCommand command, CancellationToken cancellationToken)
    {
        FunerariaEntity? funeraria = await funerarias.ObterPorIdAsync(tenantContext.RequiredTenantId, cancellationToken);
        if (funeraria is null)
        {
            return FunerariaErros.NaoEncontrada;
        }

        funeraria.AtualizarConfiguracoes(command.IdentidadeVisual!, command.Parametros!);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
