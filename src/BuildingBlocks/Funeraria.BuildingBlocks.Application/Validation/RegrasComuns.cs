using FluentValidation;

namespace Funeraria.BuildingBlocks.Application.Validation;

/// <summary>Regras de validação reutilizáveis, com as mensagens em PT-BR dos cards (INF-06).</summary>
public static class RegrasComuns
{
    private static readonly HashSet<string> Ufs = new(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    };

    public static IRuleBuilderOptions<T, string> Cpf<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(DocumentosBrasileiros.CpfValido).WithMessage("CPF não informado ou em formato inválido.");

    public static IRuleBuilderOptions<T, string> Cnpj<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(DocumentosBrasileiros.CnpjValido).WithMessage("CNPJ não informado ou em formato inválido.");

    public static IRuleBuilderOptions<T, string?> Telefone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(t => t is null || TelefoneValido(t)).WithMessage("Telefone inválido. Informe DDD e número.");

    /// <summary>Sigla de UF brasileira. Nulo é aceito: a obrigatoriedade fica com cada caso de uso.</summary>
    public static bool UfValida(string? uf) => uf is null || Ufs.Contains(uf.Trim());

    /// <summary>CEP com 8 dígitos, com ou sem hífen. Nulo é aceito.</summary>
    public static bool CepValido(string? cep) =>
        cep is null || (DocumentosBrasileiros.Normalizar(cep) is { Length: 8 } d && d.All(char.IsAsciiDigit));

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
