namespace Funeraria.Modules.Notificacoes.Contracts;

/// <summary>
/// Avisos pelo WhatsApp (NOT-03). Só sai para quem deu consentimento (opt-in) na funerária e com o canal ligado
/// (<c>WhatsApp:Habilitado</c>); fora disso a chamada não faz nada. O envio vai pela fila do Hangfire, com retry.
/// </summary>
public interface IEnvioWhatsApp
{
    Task EnfileirarAsync(MensagemWhatsAppSolicitada mensagem, CancellationToken cancellationToken = default);
}

/// <param name="IdUsuario">Conta que recebe.</param>
/// <param name="IdTenant">Funerária: o consentimento é por funerária.</param>
/// <param name="Modelo">Um dos nomes em <see cref="ModelosWhatsApp"/>.</param>
/// <param name="Parametros">Valores dos campos do template aprovado, na ordem em que aparecem nele.</param>
public sealed record MensagemWhatsAppSolicitada(Guid IdUsuario, Guid IdTenant, string Modelo, IReadOnlyList<string> Parametros);

/// <summary>
/// Modelos de mensagem. Cada um precisa de um template aprovado no WhatsApp Business, configurado em
/// <c>WhatsApp:Templates</c> (modelo → nome do template); modelo sem template não é enviado.
/// </summary>
public static class ModelosWhatsApp
{
    /// <summary>Lembrete de vencimento (COB-04). Parâmetros: nome, valor, vencimento, link.</summary>
    public const string LembreteFatura = "lembrete_fatura";

    /// <summary>Aviso da central de notificações (funeral, translado, contrato). Parâmetros: título, mensagem.</summary>
    public const string Notificacao = "notificacao";
}
