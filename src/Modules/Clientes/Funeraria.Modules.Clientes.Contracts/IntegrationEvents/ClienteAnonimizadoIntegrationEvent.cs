using Funeraria.BuildingBlocks.Application.Events;

namespace Funeraria.Modules.Clientes.Contracts.IntegrationEvents;

/// <summary>
/// Os dados pessoais do titular e dos dependentes foram anonimizados pela política de retenção (SEG-01). Cada módulo apaga o
/// que guarda dessas pessoas (ex.: o Funeral, as preferências de funeral).
/// </summary>
public sealed record ClienteAnonimizadoIntegrationEvent(Guid IdTenant, Guid IdCliente) : IntegrationEvent(IdTenant);
