using FluentValidation;

namespace Funeraria.BuildingBlocks.Application.Validation;

/// <summary>Regras de validação reutilizáveis, com as mensagens em PT-BR dos cards (INF-06).</summary>
public static class RegrasComuns
{
    public static IRuleBuilderOptions<T, string> Cpf<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(DocumentosBrasileiros.CpfValido).WithMessage("CPF não informado ou em formato inválido.");

    public static IRuleBuilderOptions<T, string> Cnpj<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(DocumentosBrasileiros.CnpjValido).WithMessage("CNPJ não informado ou em formato inválido.");

    public static IRuleBuilderOptions<T, string?> Telefone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(t => t is null || TelefoneValido(t)).WithMessage("Telefone inválido. Informe DDD e número.");

    /// <summary>DDD + número: 10 dígitos (fixo) ou 11 (celular), com ou sem pontuação.</summary>
    public static bool TelefoneValido(string telefone)
    {
        string digitos = DocumentosBrasileiros.Normalizar(telefone);
        return digitos.Length is 10 or 11 && digitos.All(char.IsAsciiDigit);
    }

    public static IRuleBuilderOptions<T, string> EmailObrigatorio<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("E-mail é obrigatório.")
            .MaximumLength(254).WithMessage("E-mail deve ter no máximo 254 caracteres.")
            .EmailAddress().WithMessage("E-mail inválido.");
}
