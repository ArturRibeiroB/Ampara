using Funeraria.BuildingBlocks.Application.Lgpd;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Contratos.Contracts;
using Funeraria.Modules.Funeral.Domain.Beneficiarios;
using Funeraria.Modules.Funeral.Domain.Funerais;
using Funeraria.Modules.Funeral.Domain.Preferencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FuneralEntity = Funeraria.Modules.Funeral.Domain.Funerais.Funeral;

namespace Funeraria.Modules.Funeral.Infrastructure.Database;

/// <summary>
/// Preferências de funeral em vida (SEG-01): valem enquanto houver contrato. Quando todos os contratos do titular estão
/// cancelados há mais de <c>DiasPreferenciasAposFimDoContrato</c>, as dele e as dos dependentes são excluídas.
/// Uma funerária por vez, no tenant dela, para a consulta a Contratos valer só para ela.
/// </summary>
internal sealed class RetencaoPreferencias(FuneralDbContext context, IServiceScopeFactory scopeFactory) : IPoliticaRetencao
{
    public string Nome => "preferencias-de-funeral";

    public async Task<int> AplicarAsync(ExecucaoRetencao execucao, CancellationToken cancellationToken)
    {
        List<Guid> funerarias = await context.Preferencias.IgnoreQueryFilters().AsNoTracking()
            .Select(p => p.TenantId).Distinct().ToListAsync(cancellationToken);

        int total = 0;
        foreach (Guid funeraria in funerarias)
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(funeraria);
            var contexto = scope.ServiceProvider.GetRequiredService<FuneralDbContext>();
            var contratos = scope.ServiceProvider.GetRequiredService<IContratosApi>();

            IReadOnlyList<Guid> semContrato = await contratos.TitularesSemContratoVigenteDesdeAsync(
                execucao.Hoje.AddDays(-execucao.Prazos.DiasPreferenciasAposFimDoContrato), cancellationToken);
            IQueryable<PreferenciaFuneral> vencidas = contexto.Preferencias.Where(p => semContrato.Contains(p.IdCliente));

            total += execucao.Simulacao
                ? await vencidas.CountAsync(cancellationToken)
                : await vencidas.ExecuteDeleteAsync(cancellationToken);
        }

        return total;
    }
}

/// <summary>
/// Dados do falecido (SEG-01): <c>AnosAposFuneral</c> depois do último funeral, com todos encerrados, beneficiário e
/// funerais são anonimizados. O evento leva Documentos a excluir os arquivos do óbito e Translado a tirar nomes e contatos.
/// Beneficiário sem funeral conta a partir da data do falecimento.
/// </summary>
internal sealed class RetencaoObitos(FuneralDbContext context, IServiceScopeFactory scopeFactory) : IPoliticaRetencao
{
    public string Nome => "obitos";

    public async Task<int> AplicarAsync(ExecucaoRetencao execucao, CancellationToken cancellationToken)
    {
        DateTime limite = execucao.Agora.AddYears(-execucao.Prazos.AnosAposFuneral);
        DateOnly limiteFalecimento = DateOnly.FromDateTime(limite);

        // Funeral aberto, ou recente, segura o beneficiário.
        var candidatos = await context.Beneficiarios.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.AnonimizadoEm == null && b.DataFalecimento < limiteFalecimento)
            .Where(b => !context.Funerais.IgnoreQueryFilters().Any(f => f.IdBeneficiario == b.Id
                && (f.DataHoraPrevista >= limite || (f.Status != StatusFuneral.Concluido && f.Status != StatusFuneral.Cancelado))))
            .OrderBy(b => b.Id)
            .Take(execucao.Prazos.LimitePorExecucao)
            .Select(b => new { b.Id, b.TenantId })
            .ToListAsync(cancellationToken);

        if (execucao.Simulacao)
        {
            return candidatos.Count;
        }

        foreach (var funeraria in candidatos.GroupBy(c => c.TenantId))
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(funeraria.Key);
            var contexto = scope.ServiceProvider.GetRequiredService<FuneralDbContext>();

            List<Guid> ids = [.. funeraria.Select(c => c.Id)];
            List<Beneficiario> beneficiarios = await contexto.Beneficiarios.Where(b => ids.Contains(b.Id)).ToListAsync(cancellationToken);
            List<FuneralEntity> funerais = await contexto.Funerais.Where(f => ids.Contains(f.IdBeneficiario)).ToListAsync(cancellationToken);

            foreach (Beneficiario beneficiario in beneficiarios)
            {
                List<FuneralEntity> doBeneficiario = [.. funerais.Where(f => f.IdBeneficiario == beneficiario.Id)];
                doBeneficiario.ForEach(f => f.AnonimizarFalecido());
                beneficiario.Anonimizar(execucao.Agora, [.. doBeneficiario.Select(f => f.Id)]);
            }

            await contexto.SaveChangesAsync(cancellationToken);
        }

        return candidatos.Count;
    }
}
