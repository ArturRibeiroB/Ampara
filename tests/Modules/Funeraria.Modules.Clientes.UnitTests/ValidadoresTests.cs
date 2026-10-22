using FluentValidation.Results;
using Funeraria.Modules.Clientes.Application.Commands.CadastrarCliente;
using Funeraria.Modules.Clientes.Application.Commands.Dependentes;

namespace Funeraria.Modules.Clientes.UnitTests;

/// <summary>As mensagens precisam ser exatamente as dos cards Criar Cliente e Criar Dependente.</summary>
public sealed class ValidadoresTests
{
    private static readonly TimeProvider Relogio = TimeProvider.System;

    [Fact]
    public void Cliente_valido_passa()
    {
        Assert.True(new CadastrarClienteCommandValidator(Relogio).Validate(Cliente()).IsValid);
    }

    [Fact]
    public void Cliente_sem_dados_usa_as_mensagens_do_card()
    {
        var vazio = new CadastrarClienteCommand("", "", null, "", "", null, null, null, null, null);

        Dictionary<string, string> erros = Mensagens(new CadastrarClienteCommandValidator(Relogio).Validate(vazio));

        Assert.Equal("Nome não informado.", erros["Nome"]);
        Assert.Equal("CPF não informado ou em formato inválido.", erros["Cpf"]);
        Assert.Equal("Data de nascimento inválida.", erros["DataNascimento"]);
        Assert.Equal("E-mail em formato inválido.", erros["Email"]);
        Assert.Equal("Senha não informada ou fora dos critérios definidos.", erros["Senha"]);
    }

    [Fact]
    public void Cliente_com_formatos_invalidos_usa_as_mensagens_do_card()
    {
        CadastrarClienteCommand invalido = Cliente() with
        {
            Cpf = "123.456.789-00",
            Email = "joao@",
            Telefone = "9999",
            Cep = "870000",
            DataNascimento = DateOnly.FromDateTime(DateTime.Today.AddDays(1))
        };

        Dictionary<string, string> erros = Mensagens(new CadastrarClienteCommandValidator(Relogio).Validate(invalido));

        Assert.Equal("CPF não informado ou em formato inválido.", erros["Cpf"]);
        Assert.Equal("E-mail em formato inválido.", erros["Email"]);
        Assert.Equal("Telefone em formato inválido.", erros["Telefone"]);
        Assert.Equal("CEP em formato inválido.", erros["Cep"]);
        Assert.Equal("Data de nascimento inválida.", erros["DataNascimento"]);
    }

    [Fact]
    public void Dependente_usa_as_mensagens_do_card()
    {
        var vazio = new CadastrarDependenteCommand("", "", null, "", "123", "nao-e-email");

        Dictionary<string, string> erros = Mensagens(new CadastrarDependenteCommandValidator(Relogio).Validate(vazio));

        Assert.Equal("Nome é obrigatório.", erros["Nome"]);
        Assert.Equal("CPF é obrigatório.", erros["Cpf"]);
        Assert.Equal("Data de nascimento inválida.", erros["DataNascimento"]);
        Assert.Equal("Parentesco é obrigatório.", erros["Parentesco"]);
        Assert.Equal("Telefone inválido.", erros["Telefone"]);
        Assert.Equal("E-mail inválido.", erros["Email"]);
    }

    [Fact]
    public void Dependente_com_cpf_invalido()
    {
        var dependente = new CadastrarDependenteCommand("Maria", "123.456.789-00", new DateOnly(1985, 8, 20), "Esposa", null, null);

        ValidationFailure erro = Assert.Single(new CadastrarDependenteCommandValidator(Relogio).Validate(dependente).Errors);

        Assert.Equal("CPF inválido.", erro.ErrorMessage);
    }

    [Fact]
    public void Dependente_sem_telefone_e_email_e_valido()
    {
        var dependente = new CadastrarDependenteCommand("Maria", "529.982.247-25", new DateOnly(1985, 8, 20), "Esposa", null, null);

        Assert.True(new CadastrarDependenteCommandValidator(Relogio).Validate(dependente).IsValid);
    }

    private static Dictionary<string, string> Mensagens(ValidationResult result) =>
        result.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.First().ErrorMessage);

    private static CadastrarClienteCommand Cliente() => new(
        "João da Silva", "529.982.247-25", new DateOnly(1985, 8, 20), "joao@email.com", "Senha@123",
        "(44) 99999-9999", "Rua Exemplo, 100", "87000-000", "Maringá", "PR");
}
