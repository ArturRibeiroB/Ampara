using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.Application.Queries.ObterPlano;

/// <summary>Detalhe de um plano da funerária atual, com as configurações (PLA-01, PLA-03).</summary>
public sealed record ObterPlanoQuery(Guid IdPlano, bool SomenteAtivos) : IQuery<PlanoResponse>;

public sealed record PlanoResponse(
    Guid IdPlano,
    string Nome,
    string? Descricao,
    decimal ValorMensal,
    decimal ValorAnual,
    decimal ValorDependenteMensal,
    decimal ValorDependenteAnual,
    int CarenciaDias,
    int LimiteDependentes,
    bool PermiteSomenteDependente,
    PlanoConfiguracoes Configuracoes,
    bool Ativo);
