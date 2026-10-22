using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Identidade.Infrastructure.Sessoes;

/// <summary>
/// Limpeza diária (AUT-03): apaga refresh tokens e links de senha vencidos ou usados há mais de 30 dias.
/// Os 30 dias de folga mantêm o histórico recente, usado para detectar reuso de refresh token roubado.
/// </summary>
internal sealed partial class LimpezaSessoesJob(IdentidadeDbContext context, TimeProvider timeProvider, ILogger<LimpezaSessoesJob> logger)
    : IJobRecorrente
{
    public static readonly TimeSpan Retencao = TimeSpan.FromDays(30);

    public string Id => "identidade-limpeza-sessoes";

    public string Cron => "0 3 * * *";

    public string Fila => FilasJobs.Manutencao;

    public async Task ExecutarAsync(CancellationToken cancellationToken)
    {
        DateTime limite = timeProvider.GetUtcNow().UtcDateTime - Retencao;

        int refreshTokens = await context.Set<RefreshToken>()
            .Where(t => t.ExpiraEm < limite || (t.RevogadoEm != null && t.RevogadoEm < limite))
            .ExecuteDeleteAsync(cancellationToken);

        int tokensSenha = await context.Set<TokenSenha>()
            .Where(t => t.ExpiraEm < limite || (t.UsadoEm != null && t.UsadoEm < limite))
            .ExecuteDeleteAsync(cancellationToken);

        LogLimpeza(logger, refreshTokens, tokensSenha);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Limpeza da Identidade: {RefreshTokens} refresh tokens e {TokensSenha} links de senha apagados")]
    private static partial void LogLimpeza(ILogger logger, int refreshTokens, int tokensSenha);
}
