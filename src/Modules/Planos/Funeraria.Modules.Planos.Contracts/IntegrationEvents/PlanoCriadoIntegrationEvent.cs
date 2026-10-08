using Funeraria.BuildingBlocks.Application.Events;

namespace Funeraria.Modules.Planos.Contracts.IntegrationEvents;

/// <summary>O Financeiro reage criando o produto e os preços no Stripe (PLA-01, COB-05).</summary>
public sealed record PlanoCriadoIntegrationEvent(
    Guid IdTenant,
    Guid IdPlano,
    string Nome,
    decimal ValorMensal,
    decimal ValorAnual,
    decimal ValorDependenteMensal,
    decimal ValorDependenteAnual)
    : IntegrationEvent(IdTenant);
