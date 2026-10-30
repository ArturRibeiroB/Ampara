using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Application.Cobrancas;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Funeraria.Modules.Financeiro.Infrastructure.Database;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Financeiro.Infrastructure.Stripe;

internal sealed class FilaEventosHangfire(IBackgroundJobClient jobs) : IFilaEventosStripe
{
    public void Enfileirar(Guid idEvento) =>
        jobs.Enqueue<ProcessarEventoStripeJob>(FilasJobs.Integracoes, j => j.ExecutarAsync(idEvento, CancellationToken.None));
}

/// <summary>
/// Processa um evento recebido pelo webhook (COB-06), fora da requisição: o Stripe recebe o 200 na hora. O tenant vem da conta
/// conectada do evento. Falhando, o Hangfire tenta de novo; o evento só fica como processado no fim.
/// </summary>
public sealed partial class ProcessarEventoStripeJob(
    IEventoStripeRepository eventos,
    IContaPagamentoRepository contas,
    ILeitorWebhook leitor,
    IFinanceiroUnitOfWork unitOfWork,
    ProcessamentoEventosStripe processamento,
    TenantContext tenantContext,
    TimeProvider timeProvider,
    ILogger<ProcessarEventoStripeJob> logger)
{
    public async Task ExecutarAsync(Guid idEvento, CancellationToken cancellationToken)
    {
        EventoStripe? evento = await eventos.ObterAsync(idEvento, cancellationToken);
        if (evento is null || evento.ProcessadoEm is not null)
        {
            return;
        }

        ContaPagamento? conta = evento.IdStripeAccount is null ? null : await contas.ObterPorIdStripeAsync(evento.IdStripeAccount, cancellationToken);
        if (conta is null)
        {
            // Evento da própria plataforma ou de uma conta que não é nossa: fica registrado, sem efeito.
            LogSemConta(logger, evento.IdEvento, evento.Tipo);
        }
        else
        {
            tenantContext.Set(conta.TenantId);
            await processamento.ProcessarAsync(leitor.Ler(evento.Conteudo), conta, cancellationToken);
        }

        evento.MarcarProcessado(timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Evento {IdEvento} ({Tipo}) sem conta conectada conhecida: ignorado")]
    private static partial void LogSemConta(ILogger logger, string idEvento, string tipo);
}

/// <summary>
/// Rotina diária da cobrança (COB-02, COB-03, COB-04, COB-08): uma funerária por vez, cada uma no próprio escopo e tenant,
/// para a falha de uma não interromper as outras. Roda às 09:00 UTC (6h em Brasília).
/// </summary>
internal sealed partial class RotinaDiariaCobrancaJob(
    FinanceiroDbContext context,
    IServiceScopeFactory scopeFactory,
    ILogger<RotinaDiariaCobrancaJob> logger)
    : IJobRecorrente
{
    public string Id => "financeiro-rotina-diaria";

    public string Cron => "0 9 * * *";

    public string Fila => FilasJobs.Integracoes;

    public async Task ExecutarAsync(CancellationToken cancellationToken)
    {
        List<Guid> funerarias = await context.Contas.IgnoreQueryFilters().AsNoTracking().Select(c => c.TenantId).ToListAsync(cancellationToken);
        foreach (Guid funeraria in funerarias)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().Set(funeraria);
                var rotinas = scope.ServiceProvider.GetRequiredService<RotinasDeCobranca>();

                // A reconciliação vem antes: inadimplência e lembretes olham as faturas já corrigidas.
                await rotinas.ReconciliarAsync(funeraria, cancellationToken);
                await rotinas.AplicarInadimplenciaAsync(funeraria, cancellationToken);
                await rotinas.EnviarLembretesAsync(funeraria, cancellationToken);
                await rotinas.AplicarReajustesAsync(funeraria, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFalha(logger, e, funeraria);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Rotina diária da cobrança falhou para a funerária {IdTenant}")]
    private static partial void LogFalha(ILogger logger, Exception e, Guid idTenant);
}
