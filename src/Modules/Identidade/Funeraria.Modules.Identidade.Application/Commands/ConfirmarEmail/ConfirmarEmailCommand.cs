using FluentValidation;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain;
using Funeraria.Modules.Identidade.Domain.Senhas;

namespace Funeraria.Modules.Identidade.Application.Commands.ConfirmarEmail;

/// <summary>Confirma o e-mail pelo link enviado após o cadastro (AUT-05). O link vale uma vez e expira.</summary>
public sealed record ConfirmarEmailCommand(string Token) : ICommand;

internal sealed class ConfirmarEmailCommandValidator : AbstractValidator<ConfirmarEmailCommand>
{
    public ConfirmarEmailCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().WithMessage(IdentidadeErros.LinkConfirmacaoInvalido.Description);
    }
}

public sealed class ConfirmarEmailCommandHandler(
    ITokenSenhaRepository tokensSenha,
    ITokenService tokens,
    IUsuarios usuarios,
    IIdentidadeUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<ConfirmarEmailCommand>
{
    public async ValueTask<Result> Handle(ConfirmarEmailCommand command, CancellationToken cancellationToken)
    {
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        TokenSenha? token = await tokensSenha.ObterPorHashAsync(tokens.Hash(command.Token), cancellationToken);
        if (token is null || !token.EstaValido(agora) || token.Finalidade != FinalidadeTokenSenha.ConfirmarEmail)
        {
            return IdentidadeErros.LinkConfirmacaoInvalido;
        }

        await usuarios.ConfirmarEmailAsync(token.IdUsuario, cancellationToken);
        token.MarcarUsado(agora);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
