using FluentValidation;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Senhas;
using Funeraria.Modules.Identidade.Domain;
using Funeraria.Modules.Identidade.Domain.Senhas;

namespace Funeraria.Modules.Identidade.Application.Commands.RedefinirSenha;

/// <summary>
/// Troca a senha pelo link do "esqueci minha senha" (AUT-05). O link vale uma vez e expira; a troca encerra as
/// sessões abertas e confirma o e-mail.
/// </summary>
public sealed record RedefinirSenhaCommand(string Token, string Senha) : ICommand;

internal sealed class RedefinirSenhaCommandValidator : AbstractValidator<RedefinirSenhaCommand>
{
    public RedefinirSenhaCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().WithMessage(IdentidadeErros.LinkRedefinicaoInvalido.Description);
        RuleFor(c => c.Senha).NotEmpty().WithMessage("Senha é obrigatória.");
    }
}

public sealed class RedefinirSenhaCommandHandler(SenhaPorLink senhaPorLink) : ICommandHandler<RedefinirSenhaCommand>
{
    public async ValueTask<Result> Handle(RedefinirSenhaCommand command, CancellationToken cancellationToken) =>
        await senhaPorLink.DefinirAsync(
            command.Token, command.Senha, FinalidadeTokenSenha.RedefinirSenha, IdentidadeErros.LinkRedefinicaoInvalido, cancellationToken);
}
