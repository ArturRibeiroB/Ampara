using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.Modules.Identidade.Application.Sessoes;

namespace Funeraria.Modules.Identidade.Application.Commands.RenovarSessao;

/// <summary>Troca o refresh token do cookie por uma sessão nova (AUT-03).</summary>
public sealed record RenovarSessaoCommand(string? RefreshToken) : ICommand<SessaoResponse>;
