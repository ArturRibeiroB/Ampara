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

    /// <summary>Prazo para o terceiro aceitar ou recusar um serviço (TER-02). Nulo se a funerária não existir.</summary>
    Task<TimeSpan?> ObterPrazoAceiteTerceiroAsync(Guid idTenant, CancellationToken cancellationToken = default);

    /// <summary>Modo da certidão de óbito da funerária (CER-02): MANUAL ou SIMULADO. Nulo se a funerária não existir.</summary>
    Task<string?> ObterModoCertidaoAsync(Guid idTenant, CancellationToken cancellationToken = default);

    /// <summary>Resumo das funerárias informadas. Ids inexistentes são ignorados.</summary>
    Task<IReadOnlyList<FunerariaResumoDto>> ListarAsync(IReadOnlyCollection<Guid> idsTenant, CancellationToken cancellationToken = default);
}

/// <param name="VencimentoEscolhidoPeloCliente">Modo ESCOLHA_CLIENTE; senão, o vencimento é o dia fixo.</param>
/// <param name="DiaVencimentoFixo">Dia do vencimento no modo FIXO.</param>
/// <param name="DiasVencimentoPermitidos">Dias que o cliente pode escolher no modo ESCOLHA_CLIENTE.</param>
/// <param name="ContratacaoLiberada">Multa, juros e desconto anual preenchidos (TEN-03).</param>
/// <param name="ToleranciaDias">Dias úteis depois do vencimento antes de suspender (COB-03).</param>
/// <param name="MultaPercentual">Multa por atraso (COB-09).</param>
/// <param name="JurosMensalPercentual">Juros de mora ao mês (COB-09).</param>
/// <param name="ReajusteAnualPercentual">Reajuste no aniversário do contrato (COB-08).</param>
/// <param name="LembretesAntes">Dias antes do vencimento em que o lembrete sai (COB-04).</param>
/// <param name="LembretesDepois">Dias depois do vencimento em que o lembrete sai (COB-04).</param>
public sealed record ParametrosCobrancaDto(
    bool VencimentoEscolhidoPeloCliente,
    int DiaVencimentoFixo,
    IReadOnlyList<int> DiasVencimentoPermitidos,
    bool ContratacaoLiberada,
    int ToleranciaDias = 1,
    decimal? MultaPercentual = null,
    decimal? JurosMensalPercentual = null,
    decimal ReajusteAnualPercentual = 0,
    IReadOnlyList<int>? LembretesAntes = null,
    IReadOnlyList<int>? LembretesDepois = null)
{
    public IReadOnlyList<int> LembretesDiasAntes => LembretesAntes ?? [];

    public IReadOnlyList<int> LembretesDiasDepois => LembretesDepois ?? [];
}

public sealed record FunerariaResumoDto(Guid IdTenant, string Slug, string NomeFantasia, bool Suspensa);

public sealed record MarcaFunerariaDto(Guid IdTenant, string Slug, string NomeExibicao, string? LogoUrl, string CorPrimaria);
