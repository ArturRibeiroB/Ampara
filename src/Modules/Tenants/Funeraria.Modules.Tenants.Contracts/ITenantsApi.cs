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

    /// <summary>Vencimento e liberação da contratação, lidos na hora (sem cache) para valer assim que o gestor salvar.</summary>
    Task<ParametrosCobrancaDto?> ObterParametrosCobrancaAsync(Guid idTenant, CancellationToken cancellationToken = default);

    /// <summary>Resumo das funerárias informadas. Ids inexistentes são ignorados.</summary>
    Task<IReadOnlyList<FunerariaResumoDto>> ListarAsync(IReadOnlyCollection<Guid> idsTenant, CancellationToken cancellationToken = default);
}

/// <param name="VencimentoEscolhidoPeloCliente">Modo ESCOLHA_CLIENTE; senão, o vencimento é o dia fixo.</param>
/// <param name="DiaVencimentoFixo">Dia do vencimento no modo FIXO.</param>
/// <param name="DiasVencimentoPermitidos">Dias que o cliente pode escolher no modo ESCOLHA_CLIENTE.</param>
/// <param name="ContratacaoLiberada">Multa, juros e desconto anual preenchidos (TEN-03).</param>
public sealed record ParametrosCobrancaDto(
    bool VencimentoEscolhidoPeloCliente,
    int DiaVencimentoFixo,
    IReadOnlyList<int> DiasVencimentoPermitidos,
    bool ContratacaoLiberada);

public sealed record FunerariaResumoDto(Guid IdTenant, string Slug, string NomeFantasia, bool Suspensa);

public sealed record MarcaFunerariaDto(Guid IdTenant, string Slug, string NomeExibicao, string? LogoUrl, string CorPrimaria);
