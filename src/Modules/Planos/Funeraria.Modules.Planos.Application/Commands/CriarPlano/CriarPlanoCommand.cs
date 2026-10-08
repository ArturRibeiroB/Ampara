using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.Application.Commands.CriarPlano;

/// <summary>Cria um plano na funerária atual (PLA-01). Devolve o id do plano.</summary>
public sealed record CriarPlanoCommand(
    string Nome,
    string? Descricao,
    decimal ValorMensal,
    decimal ValorAnual,
    decimal ValorDependenteMensal,
    decimal ValorDependenteAnual,
    int CarenciaDias,
    int LimiteDependentes,
    bool PermiteSomenteDependente,
    PlanoConfiguracoes? Configuracoes)
    : ICommand<Guid>, IPlanoDados;
