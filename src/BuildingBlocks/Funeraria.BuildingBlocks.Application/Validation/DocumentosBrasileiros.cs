namespace Funeraria.BuildingBlocks.Application.Validation;

/// <summary>Normalização e dígitos verificadores de CPF e CNPJ (INF-06).</summary>
public static class DocumentosBrasileiros
{
    private static readonly int[] PesosCnpj1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCnpj2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>Remove pontuação e espaços e deixa as letras em maiúsculas: "12.ABC.345/01DE-35" → "12ABC34501DE35".</summary>
    public static string Normalizar(string? documento) =>
        documento is null
            ? string.Empty
            : string.Concat(documento.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    public static bool CpfValido(string? cpf)
    {
        string digitos = Normalizar(cpf);
        if (digitos.Length != 11 || !digitos.All(char.IsAsciiDigit) || digitos.Distinct().Count() == 1)
        {
            return false;
        }

        return digitos[9] - '0' == DigitoCpf(digitos, 9) && digitos[10] - '0' == DigitoCpf(digitos, 10);
    }

    /// <summary>
    /// Aceita o CNPJ numérico e o alfanumérico (IN RFB 2.229/2024, em vigor desde julho de 2026):
    /// 12 caracteres de A-Z ou 0-9 e 2 dígitos verificadores numéricos. Cada caractere vale o código ASCII menos 48.
    /// </summary>
    public static bool CnpjValido(string? cnpj)
    {
        string valor = Normalizar(cnpj);
        if (valor.Length != 14
            || !valor[..12].All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c))
            || !valor[12..].All(char.IsAsciiDigit)
            || valor.Distinct().Count() == 1)
        {
            return false;
        }

        return valor[12] - '0' == DigitoCnpj(valor, PesosCnpj1) && valor[13] - '0' == DigitoCnpj(valor, PesosCnpj2);
    }

    private static int DigitoCpf(string digitos, int quantidade)
    {
        int soma = 0;
        for (int i = 0; i < quantidade; i++)
        {
            soma += (digitos[i] - '0') * (quantidade + 1 - i);
        }

        int resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static int DigitoCnpj(string valor, int[] pesos)
    {
        int soma = 0;
        for (int i = 0; i < pesos.Length; i++)
        {
            soma += (valor[i] - '0') * pesos[i];
        }

        int resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
