using Funeraria.BuildingBlocks.Application.Events;

namespace Funeraria.Modules.Identidade.Contracts.IntegrationEvents;

/// <summary>A Identidade ligou o terceiro a uma conta (TER-01). ContaAtiva: a conta já tem senha.</summary>
public sealed record TerceiroVinculadoIntegrationEvent(Guid IdTenant, Guid IdTerceiro, Guid IdUsuario, bool ContaAtiva)
    : IntegrationEvent(IdTenant);

/// <summary>A pessoa definiu a senha pelo link do e-mail: a conta passou a ser usada. Vale para todas as funerárias.</summary>
public sealed record ContaAtivadaIntegrationEvent(Guid IdUsuario) : IntegrationEvent(TenantId: null);
