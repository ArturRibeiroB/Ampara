using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Clientes.Domain.Clientes;

/// <summary>
/// Cliente titular de uma funerária (card Criar Cliente, AJU-01). O CPF é único dentro da funerária (DEC-02).
/// A senha não fica aqui: ela é da conta na Identidade, ligada pelo <see cref="IdUsuario"/>.
/// </summary>
public sealed class Cliente : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private Cliente(Guid id)
        : base(id)
    {
    }

    private Cliente()
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>Conta na plataforma. A mesma pessoa pode ser cliente de várias funerárias com a mesma conta.</summary>
    public Guid IdUsuario { get; private set; }

    public string Nome { get; private set; } = null!;

    /// <summary>Só dígitos. Não pode ser alterado pelo cliente (CLI-01).</summary>
    public string Cpf { get; private set; } = null!;

    /// <summary>Não pode ser alterada pelo cliente (CLI-01).</summary>
    public DateOnly DataNascimento { get; private set; }

    /// <summary>E-mail da conta no momento do cadastro. A troca de e-mail exige nova confirmação (AUT-05).</summary>
    public string Email { get; private set; } = null!;

    public string? Telefone { get; private set; }

    /// <summary>Logradouro e número, como no card: "Rua Exemplo, 100".</summary>
    public string? Endereco { get; private set; }

    /// <summary>Só dígitos.</summary>
    public string? Cep { get; private set; }

    public string? Cidade { get; private set; }

    /// <summary>UF com duas letras.</summary>
    public string? Estado { get; private set; }

    public static Cliente Criar(
        Guid tenantId,
        Guid idUsuario,
        string nome,
        string cpf,
        DateOnly dataNascimento,
        string email,
        string? telefone,
        string? endereco,
        string? cep,
        string? cidade,
        string? estado)
    {
        var cliente = new Cliente(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            IdUsuario = idUsuario,
            Cpf = cpf,
            DataNascimento = dataNascimento,
            Email = email.Trim()
        };

        cliente.DefinirContato(nome, telefone, endereco, cep, cidade, estado);
        cliente.Raise(new ClienteCadastradoDomainEvent(cliente.Id, idUsuario));
        return cliente;
    }

    /// <summary>O cliente atualiza os próprios dados (CLI-01). CPF, data de nascimento e e-mail ficam como estão.</summary>
    public void AtualizarDados(string nome, string? telefone, string? endereco, string? cep, string? cidade, string? estado) =>
        DefinirContato(nome, telefone, endereco, cep, cidade, estado);

    private void DefinirContato(string nome, string? telefone, string? endereco, string? cep, string? cidade, string? estado)
    {
        Nome = nome.Trim();
        Telefone = Limpar(telefone);
        Endereco = Limpar(endereco);
        Cep = Limpar(cep);
        Cidade = Limpar(cidade);
        Estado = Limpar(estado)?.ToUpperInvariant();
    }

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>A Identidade reage criando o vínculo CLIENTE da conta com a funerária.</summary>
public sealed record ClienteCadastradoDomainEvent(Guid IdCliente, Guid IdUsuario) : DomainEvent;
