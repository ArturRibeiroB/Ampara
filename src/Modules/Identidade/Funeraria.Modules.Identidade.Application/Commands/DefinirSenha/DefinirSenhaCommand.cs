using FluentValidation;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain;
using Funeraria.Modules.Identidade.Domain.Senhas;

namespace Funeraria.Modules.Identidade.Application.Commands.DefinirSenha;

/// <summary>
/// Define a senha pelo link do convite (TEN-02, AUT-07). O link só vale uma vez e expira. Definir a senha
/// confirma o e-mail. A recuperação de senha (AUT-05) vai usar o mesmo mecanismo.
/// </summary>
public sealed record DefinirSenhaCommand(string Token, string Senha) : ICommand;

internal sealed class DefinirSenhaCommandValidator : AbstractValidator<DefinirSenhaCommand>
{
    public DefinirSenhaCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().WithMessage("Link inválido ou expirado. Peça um novo convite.");
        RuleFor(c => c.Senha).NotEmpty().WithMessage("Senha é obrigatória.");
    }
}

public sealed class DefinirSenhaCommandHandler(
    ITokenSenhaRepository tokensSenha,
    ITokenService tokens,
    IUsuarios usuarios,
    IIdentidadeUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<DefinirSenhaCommand>
{
    public async ValueTask<Result> Handle(DefinirSenhaCommand command, CancellationToken cancellationToken)
    {
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        TokenSenha? token = await tokensSenha.ObterPorHashAsync(tokens.Hash(command.Token), cancellationToken);
        if (token is null || !token.EstaValido(agora) || token.Finalidade != FinalidadeTokenSenha.DefinirSenha)
        {
            return IdentidadeErros.LinkSenhaInvalido;
        }

        // Senha fraca não consome o link: a pessoa corrige e tenta de novo.
        Result definida = await usuarios.DefinirSenhaAsync(token.IdUsuario, command.Senha, cancellationToken);
        if (definida.IsFailure)
        {
            return definida;
        }

        token.MarcarUsado(agora);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
