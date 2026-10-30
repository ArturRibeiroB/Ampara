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

/// <summary>
/// O terceiro aceitou um serviço (TER-02). O módulo de origem atualiza o próprio registro.
/// Tipo: TRANSLADO, CERTIDAO ou PEDIDO.
/// </summary>
public sealed record AtribuicaoAceitaIntegrationEvent(Guid IdTenant, Guid IdAtribuicao, Guid IdTerceiro, string Tipo, Guid IdOrigem)
    : IntegrationEvent(IdTenant);

/// <summary>O terceiro recusou um serviço (TER-02). O módulo de origem atualiza o registro e a funerária escolhe outro parceiro.</summary>
public sealed record AtribuicaoRecusadaIntegrationEvent(Guid IdTenant, Guid IdAtribuicao, Guid IdTerceiro, string Tipo, Guid IdOrigem, string Motivo)
    : IntegrationEvent(IdTenant);

/// <summary>
/// Serviço atribuído a um terceiro (TER-02). As Notificações avisam o parceiro. Sem conta ligada ainda (convite pendente),
/// IdUsuarioTerceiro vem vazio.
/// </summary>
public sealed record AtribuicaoCriadaIntegrationEvent(
    Guid IdTenant, Guid IdAtribuicao, Guid IdTerceiro, Guid? IdUsuarioTerceiro, string Tipo, Guid IdOrigem, DateTime PrazoAceite)
    : IntegrationEvent(IdTenant);
