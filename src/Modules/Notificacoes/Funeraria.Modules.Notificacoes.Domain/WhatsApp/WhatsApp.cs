using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Notificacoes.Domain.WhatsApp;

/// <summary>
/// Consentimento (opt-in) para receber avisos da funerária pelo WhatsApp (NOT-03, LGPD): número, quando e de onde aceitou.
/// Um por conta e funerária. Revogado, nada mais é enviado; aceitar de novo reabre com a data e o IP novos.
/// </summary>
public sealed class ConsentimentoWhatsApp : Entity<Guid>, ITenantEntity, IAuditableEntity
{
    private ConsentimentoWhatsApp(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private ConsentimentoWhatsApp()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdUsuario { get; private set; }

    /// <summary>Formato E.164 sem o "+": 55 + DDD + número.</summary>
    public string Telefone { get; private set; } = null!;

    public DateTime AceitoEm { get; private set; }

    public string? Ip { get; private set; }

    public DateTime? RevogadoEm { get; private set; }

    public bool Ativo => RevogadoEm is null;

    public static ConsentimentoWhatsApp Aceitar(Guid tenantId, Guid idUsuario, string telefone, DateTime agora, string? ip) =>
        new(Guid.CreateVersion7(), tenantId) { IdUsuario = idUsuario, Telefone = telefone, AceitoEm = agora, Ip = ip };

    /// <summary>Novo aceite (outro número, ou depois de revogar).</summary>
    public void AceitarDeNovo(string telefone, DateTime agora, string? ip)
    {
        Telefone = telefone;
        AceitoEm = agora;
        Ip = ip;
        RevogadoEm = null;
    }

    public void Revogar(DateTime agora) => RevogadoEm ??= agora;
}

/// <summary>Mensagem enviada pelo WhatsApp, com o status de entrega que o provedor devolve (NOT-03).</summary>
public sealed class MensagemWhatsApp : Entity<Guid>, ITenantEntity, IAuditableEntity
{
    private MensagemWhatsApp(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private MensagemWhatsApp()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdUsuario { get; private set; }

    /// <summary>Modelo interno (ex.: lembrete_fatura). O template aprovado no provedor vem da configuração.</summary>
    public string Modelo { get; private set; } = null!;

    public string Telefone { get; private set; } = null!;

    /// <summary>Valores dos campos do template, na ordem ({{1}}, {{2}}...).</summary>
    public IReadOnlyList<string> Parametros { get; private set; } = [];

    public StatusMensagemWhatsApp Status { get; private set; }

    /// <summary>Id da mensagem no provedor (wamid), usado no retorno de status.</summary>
    public string? IdExterno { get; private set; }

    public string? Erro { get; private set; }

    public DateTime CriadaEm { get; private set; }

    public DateTime? AtualizadaEm { get; private set; }

    public static MensagemWhatsApp Criar(Guid tenantId, Guid idUsuario, string modelo, string telefone, IReadOnlyList<string> parametros, DateTime agora) =>
        new(Guid.CreateVersion7(), tenantId)
        {
            IdUsuario = idUsuario,
            Modelo = modelo,
            Telefone = telefone,
            Parametros = parametros,
            Status = StatusMensagemWhatsApp.Pendente,
            CriadaEm = agora
        };

    public void Enviada(string idExterno, DateTime agora)
    {
        IdExterno = idExterno;
        Status = StatusMensagemWhatsApp.Enviada;
        AtualizadaEm = agora;
    }

    /// <summary>
    /// Retorno do provedor. Os status só avançam (enviada → entregue → lida): o provedor pode mandar fora de ordem. Falha
    /// vale sempre e guarda o motivo.
    /// </summary>
    public void AtualizarStatus(StatusMensagemWhatsApp novo, string? erro, DateTime agora)
    {
        if (novo == StatusMensagemWhatsApp.Falhou)
        {
            Status = novo;
            Erro = erro;
            AtualizadaEm = agora;
            return;
        }

        if (Status != StatusMensagemWhatsApp.Falhou && novo > Status)
        {
            Status = novo;
            AtualizadaEm = agora;
        }
    }
}

public enum StatusMensagemWhatsApp
{
    Pendente = 1,
    Enviada = 2,
    Entregue = 3,
    Lida = 4,
    Falhou = 9
}

public static class WhatsAppErros
{
    public static readonly Error TelefoneInvalido =
        Error.Validacao("WhatsApp.TelefoneInvalido", "Telefone em formato inválido. Informe DDD e número.");

    public static readonly Error AceiteObrigatorio =
        Error.Validacao("WhatsApp.AceiteObrigatorio", "É preciso aceitar receber mensagens pelo WhatsApp.");

    public static readonly Error SemConsentimento =
        Error.NaoEncontrado("WhatsApp.SemConsentimento", "Você não aceitou receber mensagens pelo WhatsApp nesta funerária.");

    public static readonly Error Desligado =
        Error.Conflito("WhatsApp.Desligado", "O envio pelo WhatsApp não está disponível nesta plataforma.");
}

public interface IWhatsAppRepository
{
    /// <summary>Sem o filtro de tenant: o envio pode partir de um job; a funerária vai explícita.</summary>
    Task<ConsentimentoWhatsApp?> ObterConsentimentoAsync(Guid idTenant, Guid idUsuario, CancellationToken cancellationToken = default);

    void AdicionarConsentimento(ConsentimentoWhatsApp consentimento);

    /// <summary>Sem o filtro de tenant: o job de envio roda fora de uma requisição.</summary>
    Task<MensagemWhatsApp?> ObterMensagemAsync(Guid idMensagem, CancellationToken cancellationToken = default);

    /// <summary>Sem o filtro de tenant: o retorno do provedor só traz o id da mensagem.</summary>
    Task<MensagemWhatsApp?> ObterPorIdExternoAsync(string idExterno, CancellationToken cancellationToken = default);

    void AdicionarMensagem(MensagemWhatsApp mensagem);
}
