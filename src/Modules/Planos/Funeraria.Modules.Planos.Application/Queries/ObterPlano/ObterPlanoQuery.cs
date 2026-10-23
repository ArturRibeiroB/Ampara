using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.Application.Queries.ObterPlano;

/// <summary>Detalhe de um plano da funerária atual, com as configurações e as coberturas (PLA-01, PLA-02, PLA-03).</summary>
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
    bool Ativo,
    IReadOnlyList<CoberturaResponse> Coberturas);

/// <param name="IdPlanoCobertura">Cobertura no plano.</param>
/// <param name="IdItemCatalogo">Item do catálogo coberto.</param>
/// <param name="Nome">Nome atual do item no catálogo.</param>
/// <param name="Categoria">Categoria atual do item no catálogo.</param>
/// <param name="Quantidade">Quantidade coberta.</param>
/// <param name="KmTransladoIncluso">Quilômetros de translado incluídos, quando houver.</param>
/// <param name="ItemAtivo">Item ainda ativo no catálogo. Item desativado depois continua na cobertura.</param>
public sealed record CoberturaResponse(
    Guid IdPlanoCobertura,
    Guid IdItemCatalogo,
    string Nome,
    string Categoria,
    int Quantidade,
    int? KmTransladoIncluso,
    bool ItemAtivo);
