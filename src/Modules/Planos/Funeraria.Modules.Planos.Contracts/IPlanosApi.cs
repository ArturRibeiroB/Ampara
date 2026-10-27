namespace Funeraria.Modules.Planos.Contracts;

/// <summary>
/// API pública do módulo Planos para os outros módulos (ex.: Contratos consulta o valor vigente na contratação).
/// Respeita o tenant da execução atual.
/// </summary>
public interface IPlanosApi
{
    Task<PlanoDto?> ObterPlanoAsync(Guid idPlano, CancellationToken cancellationToken = default);

    /// <summary>Itens do catálogo cobertos pelo plano (PLA-02). Vazio se o plano não existir.</summary>
    Task<IReadOnlyList<CoberturaPlanoDto>> ObterCoberturasAsync(Guid idPlano, CancellationToken cancellationToken = default);
}

public sealed record CoberturaPlanoDto(Guid IdItemCatalogo, int Quantidade, int? KmTransladoIncluso);

public sealed record PlanoDto(
    Guid IdPlano,
    string Nome,
    decimal ValorMensal,
    decimal ValorAnual,
    decimal ValorDependenteMensal,
    decimal ValorDependenteAnual,
    int CarenciaDias,
    int LimiteDependentes,
    bool PermiteSomenteDependente,
    bool Ativo);
