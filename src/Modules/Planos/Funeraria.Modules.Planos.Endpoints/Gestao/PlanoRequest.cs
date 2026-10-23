using Funeraria.Modules.Planos.Application.Commands.DefinirCoberturas;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.Endpoints.Gestao;

/// <summary>Corpo de POST /gestao/planos e PUT /gestao/planos/{id}.</summary>
public sealed record PlanoRequest(
    string Nome,
    string? Descricao,
    decimal ValorMensal,
    decimal ValorAnual,
    decimal ValorDependenteMensal,
    decimal ValorDependenteAnual,
    int CarenciaDias,
    int LimiteDependentes,
    bool PermiteSomenteDependente,
    PlanoConfiguracoes? Configuracoes);

/// <summary>Corpo de PATCH /gestao/planos/{id}/status.</summary>
public sealed record AlterarStatusPlanoRequest(bool Ativo);

/// <summary>Corpo de PUT /gestao/planos/{id}/coberturas.</summary>
public sealed record DefinirCoberturasRequest(IReadOnlyList<CoberturaRequest>? Coberturas);

public sealed record PlanoCriadoResponse(Guid IdPlano);
