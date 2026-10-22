namespace Funeraria.Modules.Tenants.Contracts;

/// <summary>API pública do módulo Tenants. Usada pelo host para resolver o tenant da requisição (TEN-01).</summary>
public interface ITenantsApi
{
    /// <summary>Busca a funerária pelo subdomínio. O resultado é cacheado por alguns minutos.</summary>
    Task<FunerariaResumoDto?> ObterPorSlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Busca a funerária pelo id_tenant. O resultado é cacheado por alguns minutos.</summary>
    Task<FunerariaResumoDto?> ObterPorIdAsync(Guid idTenant, CancellationToken cancellationToken = default);

    /// <summary>Nome, logo e cor da funerária para e-mails e documentos (NOT-01).</summary>
    Task<MarcaFunerariaDto?> ObterMarcaAsync(Guid idTenant, CancellationToken cancellationToken = default);

    /// <summary>Resumo das funerárias informadas. Ids inexistentes são ignorados.</summary>
    Task<IReadOnlyList<FunerariaResumoDto>> ListarAsync(IReadOnlyCollection<Guid> idsTenant, CancellationToken cancellationToken = default);
}

public sealed record FunerariaResumoDto(Guid IdTenant, string Slug, string NomeFantasia, bool Suspensa);

public sealed record MarcaFunerariaDto(Guid IdTenant, string Slug, string NomeExibicao, string? LogoUrl, string CorPrimaria);
