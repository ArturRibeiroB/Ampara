namespace Funeraria.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Cache de dados lidos com frequência e que mudam pouco (INF-19), sempre separado por funerária: chave e grupo levam o
/// <c>id_tenant</c>, para uma funerária nunca ler o que é de outra. Memória local e, com Redis configurado, Redis também.
/// Quem grava invalida o grupo (a área) inteiro.
/// </summary>
public interface ICacheDaFuneraria
{
    /// <summary>Lê da funerária atual; sem valor no cache, carrega e guarda.</summary>
    /// <param name="area">Grupo invalidado junto (ex.: <see cref="AreasCache.Planos"/>).</param>
    /// <param name="chave">O que distingue a leitura dentro da área (ex.: filtros).</param>
    /// <param name="carregar">Busca no banco.</param>
    /// <param name="cancellationToken">Cancelamento.</param>
    ValueTask<T> ObterAsync<T>(string area, string chave, Func<CancellationToken, ValueTask<T>> carregar, CancellationToken cancellationToken = default);

    /// <summary>Descarta tudo da área nesta funerária.</summary>
    ValueTask InvalidarAsync(Guid idTenant, string area, CancellationToken cancellationToken = default);
}

/// <summary>Áreas do cache. Cada uma é invalidada pelo módulo dono dos dados.</summary>
public static class AreasCache
{
    public const string Planos = "planos";
    public const string Catalogo = "catalogo";

    /// <summary>Chave no cache: <c>tenant:{id}:{area}:{chave}</c>.</summary>
    public static string Chave(Guid idTenant, string area, string chave) => $"tenant:{idTenant:N}:{area}:{chave}";

    /// <summary>Grupo da área na funerária, usado para invalidar tudo de uma vez.</summary>
    public static string Grupo(Guid idTenant, string area) => $"tenant:{idTenant:N}:{area}";
}
