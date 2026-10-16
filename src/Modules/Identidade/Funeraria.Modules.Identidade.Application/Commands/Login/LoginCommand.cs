using FluentValidation;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.Modules.Identidade.Application.Sessoes;

namespace Funeraria.Modules.Identidade.Application.Commands.Login;

/// <summary>
/// Login (AUT-02). Na funerária do subdomínio, emite o token com o perfil do vínculo do usuário;
/// no domínio da plataforma, só o ADMIN entra, com token sem id_tenant.
/// </summary>
public sealed record LoginCommand(string Email, string Senha) : ICommand<SessaoResponse>;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().WithMessage("E-mail é obrigatório.");
        RuleFor(c => c.Senha).NotEmpty().WithMessage("Senha é obrigatória.");
    }
}
