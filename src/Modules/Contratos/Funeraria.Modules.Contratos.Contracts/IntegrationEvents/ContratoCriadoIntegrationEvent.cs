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
    decimal ValorTotal,
    string NomePlano,
    decimal ValorPlano,
    decimal ValorPorDependente,
    DateOnly DataContratacao)
    : IntegrationEvent(IdTenant);

/// <summary>
/// Dependente incluído ou removido (CON-02). O Financeiro atualiza a quantidade de dependentes da assinatura, sem cobrança
/// proporcional: o novo valor vale a partir da próxima fatura (COB-01).
/// </summary>
public sealed record PessoasDoContratoAlteradasIntegrationEvent(
    Guid IdTenant,
    Guid IdContrato,
    int QuantidadeDependentes,
    decimal ValorTotal)
    : IntegrationEvent(IdTenant);

/// <summary>Contrato cancelado (CON-03). O Financeiro cancela as faturas futuras; as vencidas continuam em aberto.</summary>
public sealed record ContratoCanceladoIntegrationEvent(
    Guid IdTenant,
    Guid IdContrato,
    Guid IdCliente,
    DateOnly DataCancelamento,
    string Motivo)
    : IntegrationEvent(IdTenant);
