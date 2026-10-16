using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Sessoes;

namespace Funeraria.Modules.Identidade.Application.Commands.EncerrarSessao;

/// <summary>Logout (AUT-03): revoga o refresh token atual. Sempre responde sucesso.</summary>
public sealed record EncerrarSessaoCommand(string? RefreshToken) : ICommand;

public sealed class EncerrarSessaoCommandHandler(
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    IIdentidadeUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<EncerrarSessaoCommand>
{
    public async ValueTask<Result> Handle(EncerrarSessaoCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Result.Success();
        }

        RefreshToken? token = await refreshTokens.ObterPorHashAsync(tokens.Hash(command.RefreshToken), cancellationToken);
        if (token is not null)
        {
            token.Revogar(timeProvider.GetUtcNow().UtcDateTime);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
