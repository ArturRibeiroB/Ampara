using Funeraria.BuildingBlocks.Application.Events;

namespace Funeraria.Modules.Financeiro.Contracts.IntegrationEvents;

/// <summary>Passou da tolerância com fatura em aberto (COB-03): o Contratos suspende a cobertura.</summary>
public sealed record ContratoInadimplenteIntegrationEvent(Guid IdTenant, Guid IdContrato) : IntegrationEvent(IdTenant);

/// <summary>Primeira cobrança paga ou pendências quitadas (COB-01, COB-03): o Contratos ativa ou reativa a cobertura.</summary>
public sealed record ContratoEmDiaIntegrationEvent(Guid IdTenant, Guid IdContrato) : IntegrationEvent(IdTenant);

/// <summary>A assinatura acabou no Stripe ou passou de 90 dias suspensa (COB-06, DEC-12): o Contratos cancela o contrato.</summary>
public sealed record AssinaturaEncerradaIntegrationEvent(Guid IdTenant, Guid IdContrato, string Motivo) : IntegrationEvent(IdTenant);

/// <summary>Reajuste anual aplicado (COB-08): o contrato passa a mostrar os novos valores.</summary>
public sealed record ContratoReajustadoIntegrationEvent(Guid IdTenant, Guid IdContrato, decimal ValorPlano, decimal ValorPorDependente)
    : IntegrationEvent(IdTenant);
