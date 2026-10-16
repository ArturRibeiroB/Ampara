using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Modules.Identidade.Application.Sessoes;
using Funeraria.Modules.Identidade.Domain.Vinculos;

namespace Funeraria.Modules.Identidade.UnitTests.Domain;

public sealed class UsuarioVinculoTests
{
    [Fact]
    public void Vinculo_de_equipe_nao_guarda_ids_de_cliente_nem_terceiro()
    {
        var vinculo = UsuarioVinculo.Criar(Guid.NewGuid(), Guid.NewGuid(), PerfilVinculo.Funcionario,
            idCliente: Guid.NewGuid(), idTerceiro: Guid.NewGuid(), tipoTerceiro: TiposTerceiro.Frota);

        Assert.True(vinculo.Ativo);
        Assert.Null(vinculo.IdCliente);
        Assert.Null(vinculo.IdTerceiro);
        Assert.Null(vinculo.TipoTerceiro);
    }

    [Fact]
    public void Vinculo_de_cliente_exige_o_id_do_cliente()
    {
        Assert.Throws<ArgumentException>(() => UsuarioVinculo.Criar(Guid.NewGuid(), Guid.NewGuid(), PerfilVinculo.Cliente));

        Guid idCliente = Guid.NewGuid();
        var vinculo = UsuarioVinculo.Criar(Guid.NewGuid(), Guid.NewGuid(), PerfilVinculo.Cliente, idCliente: idCliente);
        Assert.Equal(idCliente, vinculo.IdCliente);
    }

    [Fact]
    public void Vinculo_de_terceiro_exige_id_e_tipo()
    {
        Assert.Throws<ArgumentException>(() =>
            UsuarioVinculo.Criar(Guid.NewGuid(), Guid.NewGuid(), PerfilVinculo.Terceiro, idTerceiro: Guid.NewGuid()));

        var vinculo = UsuarioVinculo.Criar(Guid.NewGuid(), Guid.NewGuid(), PerfilVinculo.Terceiro,
            idTerceiro: Guid.NewGuid(), tipoTerceiro: TiposTerceiro.Fornecedor);
        Assert.Equal(TiposTerceiro.Fornecedor, vinculo.TipoTerceiro);
    }

    [Theory]
    [InlineData(PerfilVinculo.Funeraria, Perfis.Funeraria)]
    [InlineData(PerfilVinculo.Funcionario, Perfis.Funcionario)]
    [InlineData(PerfilVinculo.Cliente, Perfis.Cliente)]
    [InlineData(PerfilVinculo.Terceiro, Perfis.Terceiro)]
    public void Perfil_do_vinculo_vira_a_role_do_token(PerfilVinculo perfil, string role) =>
        Assert.Equal(role, perfil.ParaClaim());
}
