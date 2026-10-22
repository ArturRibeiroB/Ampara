using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.Modules.Notificacoes.Application.Email;
using Funeraria.Modules.Notificacoes.Contracts;
using Hangfire;

namespace Funeraria.Modules.Notificacoes.Infrastructure.Email;

/// <summary>
/// Monta o e-mail agora (com a marca da funerária de hoje) e deixa a entrega para a fila "emails" do Hangfire,
/// que tenta de novo se o SMTP falhar (NOT-01). A requisição não espera o servidor de e-mail.
/// </summary>
internal sealed class EnvioEmail(RenderizadorEmail renderizador, IBackgroundJobClient jobs) : IEnvioEmail
{
    public async Task EnfileirarAsync(EmailSolicitado email, CancellationToken cancellationToken = default)
    {
        EmailMensagem mensagem = await renderizador.RenderizarAsync(email, cancellationToken);
        jobs.Enqueue<EntregaEmailJob>(FilasJobs.Emails, job => job.EntregarAsync(mensagem, CancellationToken.None));
    }
}

/// <summary>Job da fila "emails". Falha de SMTP vira nova tentativa pela política de retry do Hangfire.</summary>
public sealed class EntregaEmailJob(IEmailTransporte transporte)
{
    [JobDisplayName("E-mail: {0}")]
    public Task EntregarAsync(EmailMensagem mensagem, CancellationToken cancellationToken) =>
        transporte.EnviarAsync(mensagem, cancellationToken);
}
