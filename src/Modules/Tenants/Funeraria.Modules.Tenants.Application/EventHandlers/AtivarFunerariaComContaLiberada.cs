using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Financeiro.Contracts.IntegrationEvents;
using Funeraria.Modules.Tenants.Application.Abstractions;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Application.EventHandlers;

/// <summary>
/// A funerária nasce PENDENTE e passa a ATIVA sozinha quando o Stripe libera a conta de pagamentos (DEC-11, TEN-02): a
/// verificação do Stripe já confere CNPJ e sócios. Suspensa continua suspensa: só o ADMIN reativa. Idempotente.
/// </summary>
internal sealed class AtivarFunerariaComContaLiberada(IFunerariaRepository funerarias, ITenantsUnitOfWork unitOfWork, ITenantCache cache)
    : IIntegrationEventHandler<ContaPagamentosLiberadaIntegrationEvent>
{
    public async Task Handle(ContaPagamentosLiberadaIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        FunerariaEntity? funeraria = await funerarias.ObterPorIdAsync(integrationEvent.IdTenant, cancellationToken);
        if (funeraria is not { Status: StatusFuneraria.Pendente })
        {
            return;
        }

        funeraria.AlterarStatus(StatusFuneraria.Ativa);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidar(funeraria.Id, funeraria.Slug);
    }
}
