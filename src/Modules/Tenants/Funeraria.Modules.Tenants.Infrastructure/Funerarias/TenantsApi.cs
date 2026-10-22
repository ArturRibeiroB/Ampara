using System.Linq.Expressions;
using Funeraria.Modules.Tenants.Application.Abstractions;
using Funeraria.Modules.Tenants.Contracts;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Modules.Tenants.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Infrastructure.Funerarias;

/// <summary>
/// Resolução de tenant usada em toda requisição: por isso cacheada. A mudança de status limpa o cache
/// (<see cref="ITenantCache"/>), para uma suspensão valer na hora.
/// </summary>
internal sealed class TenantsApi(TenantsDbContext context, IMemoryCache cache) : ITenantsApi, ITenantCache
{
    private static readonly TimeSpan Duracao = TimeSpan.FromMinutes(5);

    private static readonly Expression<Func<FunerariaEntity, FunerariaResumoDto>> Resumo =
        f => new FunerariaResumoDto(f.Id, f.Slug, f.NomeFantasia, f.Status == StatusFuneraria.Suspensa);

    public Task<FunerariaResumoDto?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        string normalizado = slug.ToLowerInvariant();
        return ObterAsync(ChaveSlug(normalizado), f => f.Slug == normalizado, cancellationToken);
    }

    public Task<FunerariaResumoDto?> ObterPorIdAsync(Guid idTenant, CancellationToken cancellationToken = default) =>
        ObterAsync(ChaveId(idTenant), f => f.Id == idTenant, cancellationToken);

    public async Task<MarcaFunerariaDto?> ObterMarcaAsync(Guid idTenant, CancellationToken cancellationToken = default)
    {
        FunerariaEntity? funeraria = await context.Funerarias.AsNoTracking().FirstOrDefaultAsync(f => f.Id == idTenant, cancellationToken);
        if (funeraria is null)
        {
            return null;
        }

        IdentidadeVisual visual = funeraria.IdentidadeVisual;
        string nome = string.IsNullOrWhiteSpace(visual.NomeExibicao) ? funeraria.NomeFantasia : visual.NomeExibicao;
        return new MarcaFunerariaDto(funeraria.Id, funeraria.Slug, nome, visual.LogoUrl, visual.CorPrimaria);
    }

    public async Task<ParametrosCobrancaDto?> ObterParametrosCobrancaAsync(Guid idTenant, CancellationToken cancellationToken = default)
    {
        ParametrosFuneraria? parametros = await context.Funerarias
            .AsNoTracking()
            .Where(f => f.Id == idTenant)
            .Select(f => f.Parametros)
            .FirstOrDefaultAsync(cancellationToken);
        if (parametros is null)
        {
            return null;
        }

        ParametrosCobranca cobranca = parametros.Cobranca;
        return new ParametrosCobrancaDto(
            cobranca.ModoVencimento == ModoVencimento.EscolhaCliente,
            cobranca.DiaVencimentoFixo,
            cobranca.DiasVencimentoPermitidos,
            cobranca.EstaCompleta());
    }

    public async Task<IReadOnlyList<FunerariaResumoDto>> ListarAsync(
        IReadOnlyCollection<Guid> idsTenant,
        CancellationToken cancellationToken = default) =>
        await context.Funerarias
            .AsNoTracking()
            .Where(f => idsTenant.Contains(f.Id))
            .OrderBy(f => f.NomeFantasia)
            .Select(Resumo)
            .ToListAsync(cancellationToken);

    public void Invalidar(Guid idTenant, string slug)
    {
        cache.Remove(ChaveId(idTenant));
        cache.Remove(ChaveSlug(slug.ToLowerInvariant()));
    }

    private async Task<FunerariaResumoDto?> ObterAsync(
        string chave,
        Expression<Func<FunerariaEntity, bool>> filtro,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(chave, out FunerariaResumoDto? cached))
        {
            return cached;
        }

        FunerariaResumoDto? funeraria = await context.Funerarias
            .AsNoTracking()
            .Where(filtro)
            .Select(Resumo)
            .FirstOrDefaultAsync(cancellationToken);

        if (funeraria is not null)
        {
            cache.Set(chave, funeraria, Duracao);
        }

        return funeraria;
    }

    private static string ChaveSlug(string slug) => $"tenants:slug:{slug}";

    private static string ChaveId(Guid idTenant) => $"tenants:id:{idTenant}";
}
