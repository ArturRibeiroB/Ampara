using Funeraria.BuildingBlocks.Application.Lgpd;
using Funeraria.BuildingBlocks.Domain.Lgpd;
using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Funeraria.BuildingBlocks.UnitTests;

/// <summary>SEG-01: o job de retenção nasce desligado, ligado roda em simulação e a falha de uma regra não para as outras.</summary>
public sealed class RetencaoDadosJobTests
{
    [Fact]
    public async Task Desligado_nao_chama_nenhuma_regra()
    {
        var regra = new RegraDeTeste("clientes");

        await Job(new RetencaoOptions(), regra).ExecutarAsync(TestContext.Current.CancellationToken);

        Assert.Empty(regra.Execucoes);
    }

    [Fact]
    public async Task Ligado_sem_mais_nada_roda_em_simulacao()
    {
        var regra = new RegraDeTeste("clientes");

        await Job(new RetencaoOptions { Habilitada = true }, regra).ExecutarAsync(TestContext.Current.CancellationToken);

        Assert.True(Assert.Single(regra.Execucoes).Simulacao);
    }

    [Fact]
    public async Task Regra_que_falha_nao_impede_as_outras()
    {
        var quebrada = new RegraDeTeste("auditoria", falhar: true);
        var depois = new RegraDeTeste("clientes");

        await Job(new RetencaoOptions { Habilitada = true, Simulacao = false }, quebrada, depois)
            .ExecutarAsync(TestContext.Current.CancellationToken);

        Assert.Single(quebrada.Execucoes);
        Assert.False(Assert.Single(depois.Execucoes).Simulacao);
    }

    [Fact]
    public void Valores_da_anonimizacao_nao_identificam_e_nao_colidem()
    {
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();

        Assert.EndsWith("@anonimizado.invalid", Anonimizacao.Email(a), StringComparison.Ordinal);
        Assert.NotEqual(Anonimizacao.Email(a), Anonimizacao.Email(b));
        Assert.NotEqual(Anonimizacao.Documento(a), Anonimizacao.Documento(b));
        Assert.DoesNotMatch("^[0-9]{11}$", Anonimizacao.Documento(a));
        Assert.Equal(new DateOnly(1985, 1, 1), Anonimizacao.DataDeNascimento(new DateOnly(1985, 8, 20)));
    }

    private static RetencaoDadosJob Job(RetencaoOptions opcoes, params IPoliticaRetencao[] regras) =>
        new(regras, Options.Create(opcoes), TimeProvider.System, NullLogger<RetencaoDadosJob>.Instance);

    private sealed class RegraDeTeste(string nome, bool falhar = false) : IPoliticaRetencao
    {
        public List<ExecucaoRetencao> Execucoes { get; } = [];

        public string Nome => nome;

        public Task<int> AplicarAsync(ExecucaoRetencao execucao, CancellationToken cancellationToken)
        {
            Execucoes.Add(execucao);
            return falhar ? throw new InvalidOperationException("falha de teste") : Task.FromResult(1);
        }
    }
}
