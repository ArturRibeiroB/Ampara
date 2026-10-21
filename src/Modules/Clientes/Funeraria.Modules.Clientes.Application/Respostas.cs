using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Clientes.Domain.Dependentes;

namespace Funeraria.Modules.Clientes.Application;

/// <summary>Dados do cliente devolvidos pela API. Nunca inclui a senha.</summary>
public sealed record ClienteResponse(
    Guid IdCliente,
    string Nome,
    string Cpf,
    DateOnly DataNascimento,
    string Email,
    string? Telefone,
    string? Endereco,
    string? Cep,
    string? Cidade,
    string? Estado)
{
    public static ClienteResponse De(Cliente c) =>
        new(c.Id, c.Nome, c.Cpf, c.DataNascimento, c.Email, c.Telefone, c.Endereco, c.Cep, c.Cidade, c.Estado);
}

public sealed record DependenteResponse(
    Guid IdDependente,
    Guid IdCliente,
    string Nome,
    string Cpf,
    DateOnly DataNascimento,
    string Parentesco,
    string? Telefone,
    string? Email)
{
    public static DependenteResponse De(Dependente d) =>
        new(d.Id, d.IdCliente, d.Nome, d.Cpf, d.DataNascimento, d.Parentesco, d.Telefone, d.Email);
}
