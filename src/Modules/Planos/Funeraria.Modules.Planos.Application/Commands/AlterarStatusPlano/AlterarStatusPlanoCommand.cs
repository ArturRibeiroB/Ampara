using Funeraria.BuildingBlocks.Application.Messaging;

namespace Funeraria.Modules.Planos.Application.Commands.AlterarStatusPlano;

/// <summary>Ativa ou desativa um plano (PLA-01). Plano com contratos não é excluído, só desativado.</summary>
public sealed record AlterarStatusPlanoCommand(Guid IdPlano, bool Ativo) : ICommand;
