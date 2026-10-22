using Funeraria.Modules.Notificacoes.Application.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Funeraria.Modules.Notificacoes.Infrastructure.Email;

/// <summary>
/// Entrega por SMTP com MailKit (NOT-01). Em desenvolvimento aponta para o Mailpit (http://localhost:8025).
/// Em produção, Resend e SendGrid oferecem SMTP: basta configurar a seção <c>Email:Smtp</c>
/// (ex.: smtp.resend.com:587, usuário "resend", senha = API key; smtp.sendgrid.net:587, usuário "apikey").
/// </summary>
internal sealed class SmtpEmailTransporte(IOptions<EmailOptions> options) : IEmailTransporte
{
    private readonly EmailOptions _options = options.Value;

    public async Task EnviarAsync(EmailMensagem mensagem, CancellationToken cancellationToken = default)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_options.NomeRemetente, _options.Remetente));
        email.To.Add(new MailboxAddress(mensagem.NomeDestinatario, mensagem.Para));
        email.Subject = mensagem.Assunto;
        email.Body = new BodyBuilder { HtmlBody = mensagem.CorpoHtml, TextBody = mensagem.CorpoTexto }.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(
            _options.Smtp.Host,
            _options.Smtp.Porta,
            _options.Smtp.UsarTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrEmpty(_options.Smtp.Usuario))
        {
            await smtp.AuthenticateAsync(_options.Smtp.Usuario, _options.Smtp.Senha ?? string.Empty, cancellationToken);
        }

        await smtp.SendAsync(email, cancellationToken);
        await smtp.DisconnectAsync(quit: true, cancellationToken);
    }
}

public sealed class EmailOptions
{
    public const string Section = "Email";

    public string Remetente { get; set; } = "nao-responda@funeraria.localhost";

    public string NomeRemetente { get; set; } = "Funerária Online";

    public SmtpOptions Smtp { get; set; } = new();

    public sealed class SmtpOptions
    {
        public string Host { get; set; } = "localhost";

        public int Porta { get; set; } = 1025;

        public bool UsarTls { get; set; }

        public string? Usuario { get; set; }

        public string? Senha { get; set; }
    }
}
