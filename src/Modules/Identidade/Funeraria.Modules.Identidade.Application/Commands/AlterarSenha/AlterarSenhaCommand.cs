using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain;
using Funeraria.Modules.Identidade.Domain.Sessoes;

namespace Funeraria.Modules.Identidade.Application.Commands.AlterarSenha;

/// <summary>
/// O usuário logado troca a própria senha informando a atual (AUT-06). A troca encerra todas as sessões da conta,
/// em todas as funerárias: quem tinha a senha antiga perde o acesso.
/// </summary>
public sealed record AlterarSenhaCommand(string SenhaAtual, string NovaSenha) : ICommand;

internal sealed class AlterarSenhaCommandValidator : AbstractValidator<AlterarSenhaCommand>
{
    public AlterarSenhaCommandValidator()
    {
        RuleFor(c => c.SenhaAtual).NotEmpty().WithMessage("Senha atual é obrigatória.");
        RuleFor(c => c.NovaSenha)
            .NotEmpty().WithMessage("Nova senha é obrigatória.")
            .NotEqual(c => c.SenhaAtual).WithMessage("A nova senha deve ser diferente da atual.");
    }
}

public sealed class AlterarSenhaCommandHandler(
    IUsuarios usuarios,
    IRefreshTokenRepository refreshTokens,
    IIdentidadeUnitOfWork unitOfWork,
    ICurrentUser usuarioAtual,
    TimeProvider timeProvider)
    : ICommandHandler<AlterarSenhaCommand>
{
    public async ValueTask<Result> Handle(AlterarSenhaCommand command, CancellationToken cancellationToken)
    {
        if (usuarioAtual.IdUsuario is not Guid idUsuario)
        {
            return IdentidadeErros.SessaoInvalida;
        }

        Result alterada = await usuarios.AlterarSenhaAsync(idUsuario, command.SenhaAtual, command.NovaSenha, cancellationToken);
        if (alterada.IsFailure)
        {
            return alterada;
        }

        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        foreach (RefreshToken sessao in await refreshTokens.ListarAtivosDoUsuarioAsync(idUsuario, agora, cancellationToken))
        {
            sessao.Revogar(agora);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
