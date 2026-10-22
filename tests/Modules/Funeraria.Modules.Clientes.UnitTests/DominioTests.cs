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
