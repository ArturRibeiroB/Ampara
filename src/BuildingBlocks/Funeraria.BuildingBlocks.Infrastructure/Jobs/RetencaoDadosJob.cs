using Funeraria.BuildingBlocks.Application.Lgpd;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Funeraria.BuildingBlocks.Infrastructure.Jobs;

/// <summary>
/// Aplica a política de retenção da LGPD (SEG-01): chama as regras de todos os módulos. Desligado por padrão
/// (<c>Lgpd:Retencao:Habilitada</c>) e, ligado, em simulação até alguém desligar <c>Lgpd:Retencao:Simulacao</c>.
/// A falha de uma regra não impede as outras. O log só traz contagens, nunca dados pessoais.
/// </summary>
public sealed partial class RetencaoDadosJob(
    IEnumerable<IPoliticaRetencao> politicas,
    IOptions<RetencaoOptions> opcoes,
    TimeProvider timeProvider,
    ILogger<RetencaoDadosJob> logger)
    : IJobRecorrente
{
    public string Id => "lgpd-retencao";

    // 04:00 UTC (1h em Brasília), longe da rotina de cobrança.
    public string Cron => "0 4 * * *";

    public string Fila => FilasJobs.Manutencao;

    public async Task ExecutarAsync(CancellationToken cancellationToken)
    {
        RetencaoOptions prazos = opcoes.Value;
        if (!prazos.Habilitada)
        {
            LogDesligada(logger);
            return;
        }

        var execucao = new ExecucaoRetencao(timeProvider.GetUtcNow().UtcDateTime, prazos.Simulacao, prazos);
        foreach (IPoliticaRetencao politica in politicas)
        {
            try
            {
                int afetados = await politica.AplicarAsync(execucao, cancellationToken);
                LogResultado(logger, politica.Nome, afetados, execucao.Simulacao ? "seriam afetados (simulação)" : "afetados");
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LogFalha(logger, e, politica.Nome);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Retenção LGPD desligada (Lgpd:Retencao:Habilitada = false)")]
    private static partial void LogDesligada(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retenção LGPD, {Politica}: {Afetados} registros {Modo}")]
    private static partial void LogResultado(ILogger logger, string politica, int afetados, string modo);

    [LoggerMessage(Level = LogLevel.Error, Message = "Retenção LGPD, {Politica}: falhou; as outras regras seguem")]
    private static partial void LogFalha(ILogger logger, Exception e, string politica);
}
