using Funeraria.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.BuildingBlocks.Infrastructure.Cache;

/// <summary>
/// HybridCache (INF-19): memória local em toda instância e, com <c>ConnectionStrings:Redis</c>, o Redis como segundo nível
/// compartilhado. Sem Redis a API funciona igual, só com a memória. A memória local vale por pouco tempo, porque a
/// invalidação feita numa instância não chega na hora à memória das outras.
/// </summary>
public static class CacheExtensions
{
    public const string ConnectionStringRedis = "Redis";

    public static IServiceCollection AddCacheDaFuneraria(this IServiceCollection services, IConfiguration configuration)
    {
        string? redis = configuration.GetConnectionString(ConnectionStringRedis);
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "funeraria:";
            });
        }

        services.AddHybridCache(o => o.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(10),
            LocalCacheExpiration = TimeSpan.FromMinutes(1)
        });
        services.AddScoped<ICacheDaFuneraria, CacheDaFuneraria>();
        return services;
    }
}

internal sealed class CacheDaFuneraria(HybridCache cache, ITenantContext tenantContext) : ICacheDaFuneraria
{
    public ValueTask<T> ObterAsync<T>(string area, string chave, Func<CancellationToken, ValueTask<T>> carregar, CancellationToken cancellationToken = default)
    {
        Guid idTenant = tenantContext.RequiredTenantId;
        return cache.GetOrCreateAsync(
            AreasCache.Chave(idTenant, area, chave),
            carregar,
            static (carregar, ct) => carregar(ct),
            tags: [AreasCache.Grupo(idTenant, area)],
            cancellationToken: cancellationToken);
    }

    public ValueTask InvalidarAsync(Guid idTenant, string area, CancellationToken cancellationToken = default) =>
        cache.RemoveByTagAsync(AreasCache.Grupo(idTenant, area), cancellationToken);
}
