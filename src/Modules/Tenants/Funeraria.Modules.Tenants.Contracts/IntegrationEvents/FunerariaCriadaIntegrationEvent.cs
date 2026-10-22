using Funeraria.BuildingBlocks.Application.Events;

namespace Funeraria.Modules.Tenants.Contracts.IntegrationEvents;

/// <summary>
/// Funerária cadastrada (TEN-02). Os outros módulos reagem em vez de o Tenants gravar nas tabelas deles (INF-01):
/// a Identidade convida o gestor e o Planos copia os planos modelo.
/// </summary>
public sealed record FunerariaCriadaIntegrationEvent(
    Guid IdTenant,
    string Slug,
    string NomeFantasia,
    string GestorNome,
    string GestorEmail)
    : IntegrationEvent(IdTenant);
