using Funeraria.BuildingBlocks.Application.Events;

namespace Funeraria.Modules.Terceiros.Contracts.IntegrationEvents;

/// <summary>
/// Terceiro cadastrado pela funerária (TER-01). A Identidade cria a conta (ou reaproveita a existente), o vínculo TERCEIRO
/// e envia o convite. Tipo: FROTA, CARTORIO_DESPACHANTE ou FORNECEDOR (valores de TiposTerceiro).
/// </summary>
public sealed record TerceiroCadastradoIntegrationEvent(Guid IdTenant, Guid IdTerceiro, string Tipo, string Nome, string Email)
    : IntegrationEvent(IdTenant);

/// <summary>Terceiro ativado ou desativado na funerária: a Identidade liga ou desliga o vínculo (TER-01).</summary>
public sealed record TerceiroStatusAlteradoIntegrationEvent(Guid IdTenant, Guid IdTerceiro, bool Ativo)
    : IntegrationEvent(IdTenant);
