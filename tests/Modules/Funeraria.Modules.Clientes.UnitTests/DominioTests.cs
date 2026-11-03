using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Clientes.Domain.Dependentes;

namespace Funeraria.Modules.Clientes.UnitTests;

public sealed class DominioTests
{
    [Fact]
    public void Cliente_novo_pede_o_vinculo_da_conta_e_normaliza_os_dados()
    {
        Guid idUsuario = Guid.NewGuid();

        Cliente cliente = Cliente.Criar(Guid.NewGuid(), idUsuario, " João ", "52998224725", new DateOnly(1985, 8, 20),
            " joao@email.com ", " ", "Rua Exemplo, 100", "87000000", "Maringá", "pr");

        Assert.Equal("João", cliente.Nome);
        Assert.Equal("joao@email.com", cliente.Email);
        Assert.Null(cliente.Telefone);
        Assert.Equal("PR", cliente.Estado);
        ClienteCadastradoDomainEvent evento = Assert.IsType<ClienteCadastradoDomainEvent>(Assert.Single(cliente.DomainEvents));
        Assert.Equal(idUsuario, evento.IdUsuario);
        Assert.Equal(cliente.Id, evento.IdCliente);
    }

    [Fact]
    public void Anonimizar_troca_os_dados_pessoais_avisa_uma_vez_e_mantem_os_ids()
    {
        Guid idUsuario = Guid.NewGuid();
        Cliente cliente = Cliente.Criar(Guid.NewGuid(), idUsuario, "João", "52998224725", new DateOnly(1985, 8, 20),
            "joao@email.com", "(44) 99999-9999", "Rua Exemplo, 100", "87000000", "Maringá", "PR");
        cliente.ClearDomainEvents();
        DateTime agora = DateTime.UtcNow;

        cliente.Anonimizar(agora);
        cliente.Anonimizar(agora.AddDays(1));

        Assert.Equal("Pessoa anonimizada", cliente.Nome);
        Assert.Equal($"anon-{cliente.Id:N}", cliente.Cpf);
        Assert.Equal($"anonimizado-{cliente.Id:N}@anonimizado.invalid", cliente.Email);
        Assert.Equal(new DateOnly(1985, 1, 1), cliente.DataNascimento);
        Assert.Null(cliente.Telefone);
        Assert.Null(cliente.Endereco);
        Assert.Null(cliente.Cep);
        Assert.Null(cliente.Cidade);
        Assert.Equal(agora, cliente.AnonimizadoEm);
        Assert.Equal(idUsuario, cliente.IdUsuario);
        ClienteAnonimizadoDomainEvent evento = Assert.IsType<ClienteAnonimizadoDomainEvent>(Assert.Single(cliente.DomainEvents));
        Assert.Equal(idUsuario, evento.IdUsuario);
    }

    [Fact]
    public void Anonimizar_dependente_tira_contato_e_documento()
    {
        Dependente dependente = Dependente.Criar(Guid.NewGuid(), Guid.NewGuid(), "Maria", "52998224725", new DateOnly(2010, 5, 3),
            "Filha", "(44) 98888-7777", "maria@email.com");

        dependente.Anonimizar(DateTime.UtcNow);

        Assert.Equal("Pessoa anonimizada", dependente.Nome);
        Assert.Equal($"anon-{dependente.Id:N}", dependente.Cpf);
        Assert.Equal(new DateOnly(2010, 1, 1), dependente.DataNascimento);
        Assert.Null(dependente.Telefone);
        Assert.Null(dependente.Email);
        Assert.Equal("Filha", dependente.Parentesco);
    }

    [Fact]
    public void Atualizar_dados_nao_muda_cpf_nem_data_de_nascimento()
    {
        Cliente cliente = Cliente.Criar(Guid.NewGuid(), Guid.NewGuid(), "João", "52998224725", new DateOnly(1985, 8, 20),
            "joao@email.com", null, null, null, null, null);

        cliente.AtualizarDados("João Silva", "(44) 99999-9999", "Rua Nova, 1", "87000000", "Maringá", "PR");

        Assert.Equal("João Silva", cliente.Nome);
        Assert.Equal("52998224725", cliente.Cpf);
        Assert.Equal(new DateOnly(1985, 8, 20), cliente.DataNascimento);
    }

    [Fact]
    public void Remocao_do_dependente_e_logica_e_guarda_a_primeira_data()
    {
        Dependente dependente = Dependente.Criar(Guid.NewGuid(), Guid.NewGuid(), "Maria", "11144477735",
            new DateOnly(1987, 1, 10), "Esposa", null, null);
        var agora = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

        dependente.Remover(agora);
        dependente.Remover(agora.AddHours(1));

        Assert.True(dependente.Removido);
        Assert.Equal(agora, dependente.RemovidoEm);
    }
}
