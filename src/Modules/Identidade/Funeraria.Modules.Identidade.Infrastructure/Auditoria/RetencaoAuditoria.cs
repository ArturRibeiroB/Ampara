using Funeraria.BuildingBlocks.Application.Lgpd;
using Funeraria.Modules.Identidade.Domain.Auditoria;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Identidade.Infrastructure.Auditoria;

/// <summary>Auditoria (AUT-09): linhas mais antigas que o prazo são excluídas (SEG-01). Vale para todas as funerárias.</summary>
internal sealed class RetencaoAuditoria(IdentidadeDbContext context) : IPoliticaRetencao
{
    public string Nome => "auditoria";

    public Task<int> AplicarAsync(ExecucaoRetencao execucao, CancellationToken cancellationToken)
    {
        DateTime limite = execucao.Agora.AddYears(-execucao.Prazos.AnosAuditoria);
        IQueryable<RegistroDeAuditoria> antigas = context.Set<RegistroDeAuditoria>().Where(a => a.DataHora < limite);

        return execucao.Simulacao
            ? antigas.CountAsync(cancellationToken)
            : antigas.ExecuteDeleteAsync(cancellationToken);
    }
}
