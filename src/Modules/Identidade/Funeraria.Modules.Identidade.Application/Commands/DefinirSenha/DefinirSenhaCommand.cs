using FluentValidation;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Senhas;
using Funeraria.Modules.Identidade.Domain;
using Funeraria.Modules.Identidade.Domain.Senhas;

namespace Funeraria.Modules.Identidade.Application.Commands.DefinirSenha;

/// <summary>
/// Define a senha pelo link do convite (TEN-02, AUT-07). O link só vale uma vez e expira. Definir a senha
/// confirma o e-mail.
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

public sealed class DefinirSenhaCommandHandler(SenhaPorLink senhaPorLink) : ICommandHandler<DefinirSenhaCommand>
{
    public async ValueTask<Result> Handle(DefinirSenhaCommand command, CancellationToken cancellationToken) =>
        await senhaPorLink.DefinirAsync(
            command.Token, command.Senha, FinalidadeTokenSenha.DefinirSenha, IdentidadeErros.LinkSenhaInvalido, cancellationToken);
}
