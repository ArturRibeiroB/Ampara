using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.BuildingBlocks.UnitTests;

public sealed class OrdenacaoTests
{
    private static readonly OrdenacaoPermitida Ordenacao = new(
        new Dictionary<string, string> { ["nome"] = "c.nome", ["cidade"] = "c.cidade" },
        padrao: "nome",
        desempate: "c.id");

    [Fact]
    public void Sem_pedido_usa_a_ordem_padrao_com_desempate() =>
        Assert.Equal("ORDER BY c.nome ASC, c.id ASC", Ordenacao.OrderBy(null).Value);

    [Theory]
    [InlineData("cidade", "desc", "ORDER BY c.cidade DESC, c.id DESC")]
    [InlineData("CIDADE", "ASC", "ORDER BY c.cidade ASC, c.id ASC")]
    [InlineData("nome", null, "ORDER BY c.nome ASC, c.id ASC")]
    public void Campo_e_direcao_permitidos(string campo, string? direcao, string esperado) =>
        Assert.Equal(esperado, Ordenacao.OrderBy(new OrdenacaoRequest(campo, direcao)).Value);

    [Theory]
    [InlineData("nome; DROP TABLE clientes", "asc", "ordenarPor")]
    [InlineData("email", "asc", "ordenarPor")]
    [InlineData("nome", "para-cima", "direcao")]
    public void Valor_fora_da_lista_vira_erro_de_validacao_e_nunca_chega_ao_sql(string campo, string direcao, string campoComErro)
    {
        Result<string> resultado = Ordenacao.OrderBy(new OrdenacaoRequest(campo, direcao));

        ValidationError erro = Assert.IsType<ValidationError>(resultado.Error);
        Assert.True(erro.Erros.ContainsKey(campoComErro));
    }

    [Fact]
    public void Pagina_e_tamanho_fora_do_limite_sao_ajustados()
    {
        PaginaRequest normalizada = new PaginaRequest(0, 1000).Normalizada();

        Assert.Equal(1, normalizada.Pagina);
        Assert.Equal(PaginaRequest.TamanhoMaximo, normalizada.Tamanho);
        Assert.Equal(0, normalizada.Deslocamento);
    }
}
