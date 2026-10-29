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
