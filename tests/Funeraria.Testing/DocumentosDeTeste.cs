namespace Funeraria.Testing;

/// <summary>Gera CPF e CNPJ válidos e únicos para os testes.</summary>
public static class DocumentosDeTeste
{
    private static readonly int[] PesosCnpj1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCnpj2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public static string NovoCnpj()
    {
        string baseCnpj = string.Concat(Enumerable.Range(0, 8).Select(_ => Random.Shared.Next(10))) + "0001";
        string com1 = baseCnpj + Digito(baseCnpj, PesosCnpj1);
        return com1 + Digito(com1, PesosCnpj2);
    }

    public static string NovoCpf()
    {
        string digitos = string.Concat(Enumerable.Range(0, 9).Select(_ => Random.Shared.Next(10)));
        digitos += DigitoCpf(digitos);
        return digitos + DigitoCpf(digitos);
    }

    private static int Digito(string valor, int[] pesos)
    {
        int resto = valor.Select((c, i) => (c - '0') * pesos[i]).Sum() % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static int DigitoCpf(string digitos)
    {
        int n = digitos.Length;
        int resto = digitos.Select((c, i) => (c - '0') * (n + 1 - i)).Sum() % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
