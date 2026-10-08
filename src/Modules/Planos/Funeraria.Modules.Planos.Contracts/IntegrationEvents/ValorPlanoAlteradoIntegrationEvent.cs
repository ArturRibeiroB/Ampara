using Funeraria.BuildingBlocks.Application.Events;

namespace Funeraria.Modules.Planos.Contracts.IntegrationEvents;

/// <summary>
/// O Financeiro reage criando preços novos no Stripe e arquivando os antigos: no Stripe, preço não é editado (PLA-01).
/// </summary>
public sealed record ValorPlanoAlteradoIntegrationEvent(
    Guid IdTenant,
    Guid IdPlano,
    decimal ValorMensal,
    decimal ValorAnual,
    decimal ValorDependenteMensal,
    decimal ValorDependenteAnual)
    : IntegrationEvent(IdTenant);
