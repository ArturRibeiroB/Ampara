using Funeraria.BuildingBlocks.Application.Events;

namespace Funeraria.Modules.Contratos.Contracts.IntegrationEvents;

/// <summary>O Financeiro reage criando a assinatura no Stripe (COB-01). Periodicidade: MENSAL ou ANUAL.</summary>
public sealed record ContratoCriadoIntegrationEvent(
    Guid IdTenant,
    Guid IdContrato,
    Guid IdCliente,
    Guid IdPlano,
    string Periodicidade,
    int DiaVencimento,
    int QuantidadeDependentes,
    decimal ValorTotal)
    : IntegrationEvent(IdTenant);
