using Funeraria.BuildingBlocks.Application.Events;

namespace Funeraria.Modules.Funeral.Contracts.IntegrationEvents;

/// <summary>
/// O funeral mudou de etapa (FUN-06). O Notificacoes avisa a família (o titular do contrato) e, no cancelamento,
/// os módulos com serviços abertos para o funeral (pedidos, translado) encerram os deles.
/// </summary>
/// <param name="IdTenant">Funerária.</param>
/// <param name="IdFuneral">Funeral.</param>
/// <param name="IdUsuarioTitular">Conta do titular do contrato, que acompanha o funeral.</param>
/// <param name="NomeFalecido">Nome do falecido, para a mensagem.</param>
/// <param name="StatusAnterior">Status de onde saiu (formato da API, ex.: REGISTRADO).</param>
/// <param name="Status">Status novo (formato da API, ex.: EM_PREPARACAO).</param>
public sealed record FuneralStatusAlteradoIntegrationEvent(
    Guid IdTenant,
    Guid IdFuneral,
    Guid? IdUsuarioTitular,
    string NomeFalecido,
    string StatusAnterior,
    string Status)
    : IntegrationEvent(IdTenant);

/// <summary>
/// Pedido ao fornecedor mudou (FUN-09). O Notificacoes avisa a funerária quando o fornecedor avança o pedido e avisa o
/// fornecedor quando o pedido é cancelado. Aceite e recusa já são avisados pelas atribuições (TER-02).
/// </summary>
/// <param name="IdTenant">Funerária.</param>
/// <param name="IdPedido">Pedido.</param>
/// <param name="IdFuneral">Funeral do pedido.</param>
/// <param name="NomeFornecedor">Razão social do fornecedor.</param>
/// <param name="IdUsuarioFornecedor">Conta do fornecedor (vazia enquanto o convite não gerou o vínculo).</param>
/// <param name="NomeFalecido">Nome do falecido, para a mensagem.</param>
/// <param name="StatusAnterior">Status de onde saiu (formato da API).</param>
/// <param name="Status">Status novo (formato da API): ACEITO, EM_PREPARO, ENTREGUE, RECUSADO ou CANCELADO.</param>
public sealed record PedidoStatusAlteradoIntegrationEvent(
    Guid IdTenant,
    Guid IdPedido,
    Guid IdFuneral,
    string NomeFornecedor,
    Guid? IdUsuarioFornecedor,
    string NomeFalecido,
    string StatusAnterior,
    string Status)
    : IntegrationEvent(IdTenant);
