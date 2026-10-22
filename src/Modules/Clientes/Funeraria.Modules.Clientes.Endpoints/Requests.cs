using System.Globalization;

namespace Funeraria.Modules.Clientes.Endpoints;

/// <summary>Corpo de POST /clientes, igual ao exemplo do card Criar Cliente.</summary>
public sealed record CadastrarClienteRequest(
    string? Nome,
    string? Cpf,
    string? DataNascimento,
    string? Telefone,
    string? Email,
    string? Senha,
    string? Endereco,
    string? Cep,
    string? Cidade,
    string? Estado);

/// <summary>Corpo de PUT /clientes/me (CLI-01). CPF, data de nascimento e e-mail não são alterados aqui.</summary>
public sealed record AtualizarMeusDadosRequest(
    string? Nome,
    string? Telefone,
    string? Endereco,
    string? Cep,
    string? Cidade,
    string? Estado);

/// <summary>Corpo de POST /dependentes, igual ao exemplo do card Criar Dependente. Não aceita id_cliente.</summary>
public sealed record CadastrarDependenteRequest(
    string? Nome,
    string? Cpf,
    string? DataNascimento,
    string? Parentesco,
    string? Telefone,
    string? Email);

/// <summary>Corpo de PUT /dependentes/{id} (CLI-02). O CPF não é alterado.</summary>
public sealed record AtualizarDependenteRequest(
    string? Nome,
    string? DataNascimento,
    string? Parentesco,
    string? Telefone,
    string? Email);

internal static class Datas
{
    /// <summary>Data no formato ISO dos cards ("1985-08-20"). Formato inválido vira nulo e a validação responde 400.</summary>
    public static DateOnly? Ler(string? valor) =>
        DateOnly.TryParseExact(valor?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly data)
            ? data
            : null;
}
