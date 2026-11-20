using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Serialization;
using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.Modules.Notificacoes.Application.WhatsApp;
using Funeraria.Modules.Notificacoes.Domain.WhatsApp;
using Funeraria.Modules.Notificacoes.Infrastructure.Notificacoes;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Options;

namespace Funeraria.Modules.Notificacoes.Infrastructure.WhatsApp;

/// <summary>
/// Seção <c>WhatsApp</c> (NOT-03). Desligado por padrão. Token, segredo do app e token de verificação vão em secret. Os templates
/// precisam estar aprovados no WhatsApp Business Manager antes de ligar.
/// </summary>
public sealed class WhatsAppOptions
{
    public const string Section = "WhatsApp";

    public bool Habilitado { get; set; }

    /// <summary>Sem token, em desenvolvimento e nos testes, usa a fake. Em produção, ligado sem token é erro no envio.</summary>
    public bool PermitirFake { get; set; }

    /// <summary>Token de acesso do WhatsApp Cloud API (secret).</summary>
    public string? TokenAcesso { get; set; }

    /// <summary>Id do número de telefone no WhatsApp Business (phone_number_id).</summary>
    public string? IdNumeroTelefone { get; set; }

    /// <summary>Segredo do app na Meta, que assina o webhook (X-Hub-Signature-256).</summary>
    public string? SegredoApp { get; set; }

    /// <summary>Token combinado na configuração do webhook, conferido na verificação do endereço.</summary>
    public string? TokenVerificacao { get; set; }

    public string UrlBase { get; set; } = "https://graph.facebook.com/v21.0/";

    public string Idioma { get; set; } = "pt_BR";

    /// <summary>Modelo interno → nome do template aprovado. Ex.: "lembrete_fatura": "lembrete_vencimento".</summary>
    public Dictionary<string, string> Templates { get; set; } = [];

    /// <summary>Tipos da central que também vão pelo WhatsApp. Ex.: FUNERAL_STATUS, CONTRATO_SUSPENSO.</summary>
    public List<string> TiposNotificacao { get; set; } = [];

    public ConfiguracaoWhatsApp Configuracao() => new(
        Habilitado,
        Idioma,
        new Dictionary<string, string>(Templates, StringComparer.Ordinal),
        new HashSet<string>(TiposNotificacao, StringComparer.Ordinal));
}

/// <summary>
/// WhatsApp Cloud API, a API oficial da Meta (NOT-03). Mensagem iniciada pela empresa só com template aprovado. Recusa por
/// número ou template (4xx) é definitiva; o resto tenta de novo pela fila. As tentativas rápidas ficam com o Polly (INF-12).
/// </summary>
internal sealed class WhatsAppCloudGateway(HttpClient http, IOptions<WhatsAppOptions> opcoes) : IGatewayWhatsApp
{
    public async Task<ResultadoEnvioWhatsApp> EnviarTemplateAsync(
        string telefone, string template, string idioma, IReadOnlyList<string> parametros, CancellationToken cancellationToken)
    {
        WhatsAppOptions o = opcoes.Value;
        if (string.IsNullOrWhiteSpace(o.TokenAcesso) || string.IsNullOrWhiteSpace(o.IdNumeroTelefone))
        {
            throw new InvalidOperationException("Configure WhatsApp:TokenAcesso e WhatsApp:IdNumeroTelefone (secrets) ou, em desenvolvimento, WhatsApp:PermitirFake.");
        }

        var corpo = new
        {
            messaging_product = "whatsapp",
            to = telefone,
            type = "template",
            template = new
            {
                name = template,
                language = new { code = idioma },
                components = new object[]
                {
                    new { type = "body", parameters = parametros.Select(p => new { type = "text", text = p }).ToArray() }
                }
            }
        };

        using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(o.UrlBase), $"{o.IdNumeroTelefone}/messages"))
        {
            Content = JsonContent.Create(corpo)
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.TokenAcesso);

        using HttpResponseMessage resposta = await http.SendAsync(pedido, cancellationToken);
        string texto = await resposta.Content.ReadAsStringAsync(cancellationToken);
        if (resposta.IsSuccessStatusCode)
        {
            string? id = JsonNode.Parse(texto)?["messages"]?[0]?["id"]?.GetValue<string>();
            return new ResultadoEnvioWhatsApp(id, id is null ? "Resposta sem id da mensagem." : null);
        }

        bool definitivo = resposta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Forbidden;
        string? erro = JsonNode.Parse(texto)?["error"]?["message"]?.GetValue<string>() ?? resposta.StatusCode.ToString();
        return new ResultadoEnvioWhatsApp(null, erro, definitivo);
    }
}

/// <summary>WhatsApp em memória, para testes e desenvolvimento sem token. Guarda o que seria enviado.</summary>
public sealed class WhatsAppFake : IGatewayWhatsApp
{
    private readonly ConcurrentQueue<(string Telefone, string Template, IReadOnlyList<string> Parametros, string IdExterno)> _enviadas = new();
    private int _sequencia;

    public IReadOnlyCollection<(string Telefone, string Template, IReadOnlyList<string> Parametros, string IdExterno)> Enviadas => _enviadas;

    public Task<ResultadoEnvioWhatsApp> EnviarTemplateAsync(
        string telefone, string template, string idioma, IReadOnlyList<string> parametros, CancellationToken cancellationToken)
    {
        string id = $"wamid.teste{Interlocked.Increment(ref _sequencia)}";
        _enviadas.Enqueue((telefone, template, parametros, id));
        return Task.FromResult(new ResultadoEnvioWhatsApp(id));
    }
}

internal sealed class FilaWhatsAppHangfire(IBackgroundJobClient jobs) : IFilaWhatsApp
{
    public void Enfileirar(Guid idMensagem) =>
        jobs.Enqueue<EntregaWhatsAppJob>(FilasJobs.Integracoes, j => j.EntregarAsync(idMensagem, CancellationToken.None));
}

/// <summary>Job da fila "integracoes". Falha temporária do provedor vira nova tentativa pela política de retry do Hangfire.</summary>
public sealed class EntregaWhatsAppJob(EntregaWhatsApp entrega)
{
    [JobDisplayName("WhatsApp: {0}")]
    public Task EntregarAsync(Guid idMensagem, CancellationToken cancellationToken) => entrega.EntregarAsync(idMensagem, cancellationToken);
}

/// <summary>
/// Webhook da Meta: a assinatura é o HMAC-SHA256 do corpo com o segredo do app, no cabeçalho <c>X-Hub-Signature-256</c>
/// ("sha256=..."). Só os status de entrega são lidos; mensagens recebidas dos clientes não são tratadas aqui.
/// </summary>
internal sealed class LeitorWebhookWhatsApp(IOptions<WhatsAppOptions> opcoes) : ILeitorWebhookWhatsApp
{
    public IReadOnlyList<StatusWhatsAppRecebido>? Ler(string corpo, string? assinatura)
    {
        string? segredo = opcoes.Value.SegredoApp;
        if (string.IsNullOrWhiteSpace(segredo) || assinatura is null || !assinatura.StartsWith("sha256=", StringComparison.Ordinal))
        {
            return null;
        }

        byte[] esperado = HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo), Encoding.UTF8.GetBytes(corpo));
        byte[] recebido;
        try
        {
            recebido = Convert.FromHexString(assinatura["sha256=".Length..]);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(esperado, recebido))
        {
            return null;
        }

        try
        {
            return [.. Status(JsonNode.Parse(corpo))];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public string? Verificar(string? modo, string? token, string? desafio) =>
        modo == "subscribe" && !string.IsNullOrWhiteSpace(opcoes.Value.TokenVerificacao) && token == opcoes.Value.TokenVerificacao
            ? desafio
            : null;

    private static IEnumerable<StatusWhatsAppRecebido> Status(JsonNode? raiz)
    {
        foreach (JsonNode? entrada in raiz?["entry"]?.AsArray() ?? [])
        {
            foreach (JsonNode? mudanca in entrada?["changes"]?.AsArray() ?? [])
            {
                foreach (JsonNode? status in mudanca?["value"]?["statuses"]?.AsArray() ?? [])
                {
                    string? id = status?["id"]?.GetValue<string>();
                    StatusMensagemWhatsApp? lido = status?["status"]?.GetValue<string>() switch
                    {
                        "sent" => StatusMensagemWhatsApp.Enviada,
                        "delivered" => StatusMensagemWhatsApp.Entregue,
                        "read" => StatusMensagemWhatsApp.Lida,
                        "failed" => StatusMensagemWhatsApp.Falhou,
                        _ => null
                    };
                    if (id is not null && lido is StatusMensagemWhatsApp s)
                    {
                        yield return new StatusWhatsAppRecebido(id, s, status?["errors"]?[0]?["title"]?.GetValue<string>());
                    }
                }
            }
        }
    }
}

internal sealed class ConsentimentoWhatsAppConfiguration : IEntityTypeConfiguration<ConsentimentoWhatsApp>
{
    public void Configure(EntityTypeBuilder<ConsentimentoWhatsApp> builder)
    {
        builder.ToTable("consentimentos_whatsapp");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id_consentimento").ValueGeneratedNever();
        builder.Property(c => c.Telefone).HasMaxLength(15);
        builder.Property(c => c.Ip).HasMaxLength(45);
        builder.HasIndex(c => new { c.TenantId, c.IdUsuario }).IsUnique();
    }
}

internal sealed class MensagemWhatsAppConfiguration : IEntityTypeConfiguration<MensagemWhatsApp>
{
    public void Configure(EntityTypeBuilder<MensagemWhatsApp> builder)
    {
        builder.ToTable("mensagens_whatsapp");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id_mensagem").ValueGeneratedNever();
        builder.Property(m => m.Modelo).HasMaxLength(60);
        builder.Property(m => m.Telefone).HasMaxLength(15);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.IdExterno).HasMaxLength(200);
        builder.Property(m => m.Erro).HasMaxLength(500);
        builder.Property(m => m.Parametros)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonbOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonbOptions.Default) ?? new List<string>(),
                new ValueComparer<IReadOnlyList<string>>((a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x)), v => v.ToList()));
        builder.HasIndex(m => m.IdExterno);
        builder.HasIndex(m => new { m.TenantId, m.IdUsuario, m.CriadaEm });
    }
}

internal sealed class WhatsAppRepository(NotificacoesDbContext context) : IWhatsAppRepository
{
    public Task<ConsentimentoWhatsApp?> ObterConsentimentoAsync(Guid idTenant, Guid idUsuario, CancellationToken cancellationToken = default) =>
        context.Set<ConsentimentoWhatsApp>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == idTenant && c.IdUsuario == idUsuario, cancellationToken);

    public void AdicionarConsentimento(ConsentimentoWhatsApp consentimento) => context.Set<ConsentimentoWhatsApp>().Add(consentimento);

    public Task<MensagemWhatsApp?> ObterMensagemAsync(Guid idMensagem, CancellationToken cancellationToken = default) =>
        context.Set<MensagemWhatsApp>().IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == idMensagem, cancellationToken);

    public Task<MensagemWhatsApp?> ObterPorIdExternoAsync(string idExterno, CancellationToken cancellationToken = default) =>
        context.Set<MensagemWhatsApp>().IgnoreQueryFilters().FirstOrDefaultAsync(m => m.IdExterno == idExterno, cancellationToken);

    public void AdicionarMensagem(MensagemWhatsApp mensagem) => context.Set<MensagemWhatsApp>().Add(mensagem);
}
