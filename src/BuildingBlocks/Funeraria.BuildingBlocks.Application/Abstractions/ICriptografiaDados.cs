namespace Funeraria.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Dados pessoais cifrados em repouso (SEG-01, LGPD). A cifra é aleatória (o mesmo CPF gera textos diferentes), por isso
/// busca e unicidade usam o <see cref="Hash"/>, que é sempre o mesmo para o mesmo valor.
/// </summary>
public interface ICriptografiaDados
{
    string Cifrar(string valor);

    /// <summary>Volta ao valor original. Aceita valor antigo ainda sem cifra (antes da migração dos dados).</summary>
    string Decifrar(string valor);

    /// <summary>HMAC-SHA256 com chave secreta: permite índice único e busca exata sem guardar o valor.</summary>
    string Hash(string valor);
}

/// <summary>Máscaras para respostas que não precisam do dado completo (SEG-01).</summary>
public static class Mascaras
{
    /// <summary>"12345678909" → "***.456.789-**".</summary>
    public static string Cpf(string? cpf) =>
        cpf is { Length: 11 } ? $"***.{cpf[3..6]}.{cpf[6..9]}-**" : "***";
}
