using Funeraria.BuildingBlocks.Application.Lgpd;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Clientes.Domain.Dependentes;
using Funeraria.Modules.Contratos.Contracts;
using Funeraria.Modules.Identidade.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Clientes.Infrastructure.Database;

/// <summary>
/// Política de retenção dos clientes (SEG-01, <c>docs/lgpd.md</c>). Anonimiza titular e dependentes quando:
/// <list type="bullet">
/// <item>todos os contratos do titular estão cancelados e o último terminou há mais de <c>AnosAposFimDoContrato</c>; ou</item>
/// <item>o titular nunca teve contrato, foi cadastrado há mais de <c>AnosClienteSemContrato</c> e não entra na conta desde então.</item>
/// </list>
/// Uma funerária por vez, no tenant dela, para as consultas a Contratos valerem só para ela.
/// </summary>
internal sealed class RetencaoClientes(ClientesDbContext context, IServiceScopeFactory scopeFactory) : IPoliticaRetencao
{
    public string Nome => "clientes";

    public async Task<int> AplicarAsync(ExecucaoRetencao execucao, CancellationToken cancellationToken)
    {
        List<Guid> funerarias = await context.Clientes.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.AnonimizadoEm == null)
            .Select(c => c.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        int total = 0;
        foreach (Guid funeraria in funerarias)
        {
            int restante = execucao.Prazos.LimitePorExecucao - total;
            if (restante <= 0)
            {
                break;
            }

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(funeraria);
            total += await scope.ServiceProvider.GetRequiredService<AnonimizacaoDeClientes>().AplicarAsync(execucao, restante, cancellationToken);
        }

        return total;
    }
}

/// <summary>A política aplicada na funerária do escopo.</summary>
internal sealed class AnonimizacaoDeClientes(ClientesDbContext context, IContratosApi contratos, IIdentidadeApi identidade)
{
    public async Task<int> AplicarAsync(ExecucaoRetencao execucao, int limite, CancellationToken cancellationToken)
    {
        List<Guid> candidatos = [.. await ContratosEncerradosAsync(execucao, cancellationToken), .. await SemContratoNemAcessoAsync(execucao, cancellationToken)];
        candidatos = [.. candidatos.Distinct().Take(limite)];
        if (execucao.Simulacao || candidatos.Count == 0)
        {
            return candidatos.Count;
        }

        List<Cliente> clientes = await context.Clientes.Where(c => candidatos.Contains(c.Id)).ToListAsync(cancellationToken);
        // Removidos também: o histórico de contratos e funerais aponta para eles.
        List<Dependente> dependentes = await context.Dependentes.Where(d => candidatos.Contains(d.IdCliente)).ToListAsync(cancellationToken);
        foreach (Cliente cliente in clientes)
        {
            cliente.Anonimizar(execucao.Agora);
        }

        foreach (Dependente dependente in dependentes)
        {
            dependente.Anonimizar(execucao.Agora);
        }

        await context.SaveChangesAsync(cancellationToken);
        return clientes.Count;
    }

    private async Task<List<Guid>> ContratosEncerradosAsync(ExecucaoRetencao execucao, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> titulares = await contratos.TitularesSemContratoVigenteDesdeAsync(
            execucao.Hoje.AddYears(-execucao.Prazos.AnosAposFimDoContrato), cancellationToken);

        return await context.Clientes.AsNoTracking()
            .Where(c => c.AnonimizadoEm == null && titulares.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> SemContratoNemAcessoAsync(ExecucaoRetencao execucao, CancellationToken cancellationToken)
    {
        DateTime limite = execucao.Agora.AddYears(-execucao.Prazos.AnosClienteSemContrato);
        HashSet<Guid> comContrato = [.. await contratos.ClientesComContratoAsync(null, cancellationToken)];

        var antigos = (await context.Clientes.AsNoTracking()
                .Where(c => c.AnonimizadoEm == null && EF.Property<DateTime>(c, ModelBuilderExtensions.CreatedAt) < limite)
                .Select(c => new { c.Id, c.IdUsuario })
                .ToListAsync(cancellationToken))
            .Where(c => !comContrato.Contains(c.Id))
            .ToList();
        if (antigos.Count == 0)
        {
            return [];
        }

        // Conta que entrou dentro do prazo (mesmo que em outra funerária) mostra que a pessoa ainda usa a plataforma.
        IReadOnlyDictionary<Guid, DateTime> acessos = await identidade.UltimosAcessosAsync([.. antigos.Select(c => c.IdUsuario)], cancellationToken);
        return [.. antigos.Where(c => !acessos.TryGetValue(c.IdUsuario, out DateTime acesso) || acesso < limite).Select(c => c.Id)];
    }
}
