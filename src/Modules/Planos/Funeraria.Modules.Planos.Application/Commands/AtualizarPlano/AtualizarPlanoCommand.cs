using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.Application.Commands.AtualizarPlano;

/// <summary>Edita um plano (PLA-01). Novo valor vale só para novos contratos.</summary>
public sealed record AtualizarPlanoCommand(
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
    PlanoConfiguracoes? Configuracoes)
    : ICommand, IPlanoDados;
