namespace Funeraria.Modules.Notificacoes.Contracts;

/// <summary>
/// Envio de e-mails transacionais (NOT-01). Quem chama deve fazer isso a partir de um handler de evento
/// (outbox), para o envio ter retry e não atrasar a requisição.
/// </summary>
public interface IEmailSender
{
    Task EnviarAsync(EmailMensagem mensagem, CancellationToken cancellationToken = default);
}

public sealed record EmailMensagem(string Para, string NomeDestinatario, string Assunto, string CorpoHtml, string CorpoTexto);
