using Microsoft.AspNetCore.Identity;

namespace Funeraria.Modules.Identidade.Infrastructure.Usuarios;

/// <summary>Mensagens do Identity em PT-BR (INF-06). As de senha aparecem para o usuário ao definir a senha.</summary>
internal sealed class IdentityErrosPtBr : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Erro(nameof(DefaultError), "Ocorreu um erro inesperado.");

    public override IdentityError PasswordMismatch() =>
        Erro(nameof(PasswordMismatch), "Senha incorreta.");

    public override IdentityError PasswordTooShort(int length) =>
        Erro(nameof(PasswordTooShort), $"A senha deve ter pelo menos {length} caracteres.");

    public override IdentityError PasswordRequiresDigit() =>
        Erro(nameof(PasswordRequiresDigit), "A senha deve ter pelo menos um número.");

    public override IdentityError PasswordRequiresLower() =>
        Erro(nameof(PasswordRequiresLower), "A senha deve ter pelo menos uma letra minúscula.");

    public override IdentityError PasswordRequiresUpper() =>
        Erro(nameof(PasswordRequiresUpper), "A senha deve ter pelo menos uma letra maiúscula.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Erro(nameof(PasswordRequiresNonAlphanumeric), "A senha deve ter pelo menos um caractere especial (ex.: @, #, !).");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Erro(nameof(PasswordRequiresUniqueChars), $"A senha deve ter pelo menos {uniqueChars} caracteres diferentes.");

    public override IdentityError DuplicateEmail(string email) =>
        Erro(nameof(DuplicateEmail), "E-mail já cadastrado.");

    public override IdentityError DuplicateUserName(string userName) =>
        Erro(nameof(DuplicateUserName), "E-mail já cadastrado.");

    public override IdentityError InvalidEmail(string? email) =>
        Erro(nameof(InvalidEmail), "E-mail inválido.");

    public override IdentityError InvalidUserName(string? userName) =>
        Erro(nameof(InvalidUserName), "E-mail inválido.");

    private static IdentityError Erro(string code, string description) => new() { Code = code, Description = description };
}
