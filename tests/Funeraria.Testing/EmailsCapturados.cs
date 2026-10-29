using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Funeraria.Modules.Notificacoes.Application.Email;

namespace Funeraria.Testing;

/// <summary>
/// Substitui a entrega por SMTP nos testes e guarda as mensagens já renderizadas pelos modelos.
/// O e-mail passa pela fila do Hangfire, como em produção.
/// </summary>
public sealed partial class EmailsCapturados : IEmailTransporte
{
    private readonly ConcurrentQueue<EmailMensagem> _emails = new();

    public Task EnviarAsync(EmailMensagem mensagem, CancellationToken cancellationToken = default)
    {
        _emails.Enqueue(mensagem);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMensagem> Para(string email) =>
        _emails.Where(e => string.Equals(e.Para, email, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Espera o e-mail chegar: ele é enviado em background, pelo outbox.</summary>
    public async Task<EmailMensagem> AguardarAsync(string para, Func<EmailMensagem, bool>? filtro = null, TimeSpan? timeout = null)
    {
        // 30 s: com a suíte inteira em paralelo, a fila do Hangfire às vezes passa dos 15 s.
        DateTime limite = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (DateTime.UtcNow < limite)
        {
            EmailMensagem? email = Para(para).FirstOrDefault(e => filtro?.Invoke(e) ?? true);
            if (email is not null)
            {
                return email;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Nenhum e-mail para {para}.");
    }

    /// <summary>Token do link de definir senha que veio no corpo do e-mail.</summary>
    public static string TokenDoLink(EmailMensagem email)
    {
        Match match = TokenRegex().Match(email.CorpoTexto);
        return match.Success
            ? Uri.UnescapeDataString(match.Groups[1].Value)
            : throw new InvalidOperationException("O e-mail não tem link com token.");
    }

    [GeneratedRegex(@"[?&]token=([^\s&""]+)")]
    private static partial Regex TokenRegex();
}
