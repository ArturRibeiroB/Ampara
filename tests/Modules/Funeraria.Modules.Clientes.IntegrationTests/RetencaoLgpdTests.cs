using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Lgpd;
using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Funeraria.Modules.Clientes.IntegrationTests.ApiCliente;

namespace Funeraria.Modules.Clientes.IntegrationTests;

/// <summary>
/// SEG-01: política de retenção dos clientes. As datas são empurradas para o passado direto no banco; a regra roda como o job
/// a rodaria. Cada teste confere os próprios registros: a regra vale para todas as funerárias do banco de teste.
/// </summary>
[Collection(ApiCollection.Nome)]
public sealed class RetencaoLgpdTests(ClientesApiFactory factory)
{
    private const string NomeOriginal = "João da Silva";

    [Fact]
    public async Task Job_desligado_por_padrao_nao_toca_em_nada()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        await SemContratoNemAcessoHaAnosAsync(cliente.IdCliente, anos: 3);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IJobRecorrente job = scope.ServiceProvider.GetServices<IJobRecorrente>().Single(j => j.Id == "lgpd-retencao");
        await job.ExecutarAsync(Ct);

        Assert.Equal(NomeOriginal, (await LerClienteAsync(cliente.IdCliente)).Nome);
    }

    [Fact]
    public async Task Simulacao_conta_o_cliente_mas_nao_altera_nada()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        await SemContratoNemAcessoHaAnosAsync(cliente.IdCliente, anos: 3);

        int contados = await AplicarAsync(simulacao: true);

        Assert.True(contados >= 1);
        ClienteGravado gravado = await LerClienteAsync(cliente.IdCliente);
        Assert.Equal(NomeOriginal, gravado.Nome);
        Assert.Null(gravado.AnonimizadoEm);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(funeraria.Slug, cliente.Email)).StatusCode);
    }

    [Fact]
    public async Task Titular_com_contratos_encerrados_ha_mais_de_cinco_anos_e_anonimizado_com_os_dependentes_e_perde_o_acesso()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);
        Guid contrato = await cliente.ContratarAsync(plano);
        await ExecutarAsync(
            "UPDATE contratos.contratos SET status = 'Cancelado', data_fim = current_date - interval '6 years' WHERE id_contrato = @contrato",
            new { contrato });

        await AplicarAsync(simulacao: false);

        ClienteGravado titular = await LerClienteAsync(cliente.IdCliente);
        Assert.Equal("Pessoa anonimizada", titular.Nome);
        Assert.Equal($"anonimizado-{cliente.IdCliente:N}@anonimizado.invalid", titular.Email);
        Assert.Null(titular.Telefone);
        Assert.Null(titular.Endereco);
        Assert.NotNull(titular.AnonimizadoEm);
        Assert.Equal($"anon-{cliente.IdCliente:N}", titular.CpfOriginal);
        Assert.Equal(factory.Services.GetRequiredService<ICriptografiaDados>().Hash(titular.CpfOriginal!), titular.CpfHash);

        (string nomeDependente, DateTime? dependenteAnonimizado) = await LerDependenteAsync(cliente.Dependentes[0]);
        Assert.Equal("Pessoa anonimizada", nomeDependente);
        Assert.NotNull(dependenteAnonimizado);

        // Pelo outbox: a Identidade desativa o vínculo e, como a conta só servia a este cadastro, anonimiza a conta.
        await AguardarAsync(async () => await ContaAnonimizadaAsync(cliente.IdCliente));
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(funeraria.Slug, cliente.Email)).StatusCode);
    }

    [Fact]
    public async Task Contrato_vigente_contrato_encerrado_ha_pouco_e_cliente_que_entrou_recentemente_ficam_como_estao()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste vigente = await funeraria.CriarClienteAsync();
        await vigente.ContratarAsync(plano);
        ClienteDeTeste encerradoHaPouco = await funeraria.CriarClienteAsync();
        Guid contrato = await encerradoHaPouco.ContratarAsync(plano);
        await ExecutarAsync(
            "UPDATE contratos.contratos SET status = 'Cancelado', data_fim = current_date - interval '1 year' WHERE id_contrato = @contrato",
            new { contrato });
        ClienteDeTeste ativoNaConta = await funeraria.CriarClienteAsync();
        await ExecutarAsync(
            "UPDATE clientes.clientes SET created_at = now() - interval '3 years' WHERE id_cliente = @id", new { id = ativoNaConta.IdCliente });

        await AplicarAsync(simulacao: false);

        foreach (Guid id in new[] { vigente.IdCliente, encerradoHaPouco.IdCliente, ativoNaConta.IdCliente })
        {
            ClienteGravado gravado = await LerClienteAsync(id);
            Assert.Equal(NomeOriginal, gravado.Nome);
            Assert.Null(gravado.AnonimizadoEm);
        }
    }

    [Fact]
    public async Task Cliente_sem_contrato_e_sem_acesso_ha_mais_de_dois_anos_e_anonimizado()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        await SemContratoNemAcessoHaAnosAsync(cliente.IdCliente, anos: 3);

        await AplicarAsync(simulacao: false);

        Assert.Equal("Pessoa anonimizada", (await LerClienteAsync(cliente.IdCliente)).Nome);
        await AguardarAsync(async () => await ContaAnonimizadaAsync(cliente.IdCliente));
    }

    private async Task<int> AplicarAsync(bool simulacao)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IPoliticaRetencao politica = scope.ServiceProvider.GetServices<IPoliticaRetencao>().Single(p => p.Nome == "clientes");
        return await politica.AplicarAsync(new ExecucaoRetencao(DateTime.UtcNow, simulacao, new RetencaoOptions()), Ct);
    }

    private Task SemContratoNemAcessoHaAnosAsync(Guid idCliente, int anos) =>
        ExecutarAsync(
            $"""
            UPDATE clientes.clientes SET created_at = now() - interval '{anos} years' WHERE id_cliente = @idCliente;
            UPDATE identidade.usuarios SET criado_em = now() - interval '{anos} years', ultimo_acesso_em = now() - interval '{anos} years'
            WHERE id_usuario = (SELECT id_usuario FROM clientes.clientes WHERE id_cliente = @idCliente);
            """,
            new { idCliente });

    private async Task<bool> ContaAnonimizadaAsync(Guid idCliente)
    {
        await using DbConnection conexao = await AbrirAsync();
        return await conexao.QuerySingleAsync<bool>(new CommandDefinition(
            """
            SELECT NOT v.ativo AND u.email LIKE 'anonimizado-%@anonimizado.invalid' AND NOT u.ativo AND u.password_hash IS NULL
            FROM clientes.clientes c
            JOIN identidade.usuarios u ON u.id_usuario = c.id_usuario
            JOIN identidade.usuario_vinculos v ON v.id_usuario = c.id_usuario AND v.id_tenant = c.id_tenant
            WHERE c.id_cliente = @idCliente
            """,
            new { idCliente }, cancellationToken: Ct));
    }

    private async Task<HttpResponseMessage> LoginAsync(string slug, string email) =>
        await factory.Cliente(slug).PostAsJsonAsync("/api/v1/auth/login", new { email, senha = FunerariaApiFactory.SenhaPadrao }, Json, Ct);

    private async Task<ClienteGravado> LerClienteAsync(Guid id)
    {
        await using DbConnection conexao = await AbrirAsync();
        ClienteGravado gravado = await conexao.QuerySingleAsync<ClienteGravado>(new CommandDefinition(
            """
            SELECT nome AS Nome, email AS Email, telefone AS Telefone, endereco AS Endereco, cpf AS Cpf, cpf_hash AS CpfHash,
                   anonimizado_em AS AnonimizadoEm
            FROM clientes.clientes WHERE id_cliente = @id
            """,
            new { id }, cancellationToken: Ct));
        return gravado with { CpfOriginal = factory.Services.GetRequiredService<ICriptografiaDados>().Decifrar(gravado.Cpf) };
    }

    private async Task<(string Nome, DateTime? AnonimizadoEm)> LerDependenteAsync(Guid id)
    {
        await using DbConnection conexao = await AbrirAsync();
        return await conexao.QuerySingleAsync<(string, DateTime?)>(new CommandDefinition(
            "SELECT nome, anonimizado_em FROM clientes.dependentes WHERE id_dependente = @id", new { id }, cancellationToken: Ct));
    }

    private async Task ExecutarAsync(string sql, object parametros)
    {
        await using DbConnection conexao = await AbrirAsync();
        await conexao.ExecuteAsync(new CommandDefinition(sql, parametros, cancellationToken: Ct));
    }

    private ValueTask<DbConnection> AbrirAsync() => factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);

    private static async Task AguardarAsync(Func<Task<bool>> condicao)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        limite.CancelAfter(TimeSpan.FromSeconds(30));
        while (!await condicao())
        {
            await Task.Delay(200, limite.Token);
        }
    }

    private sealed record ClienteGravado
    {
        public required string Nome { get; init; }

        public required string Email { get; init; }

        public string? Telefone { get; init; }

        public string? Endereco { get; init; }

        public required string Cpf { get; init; }

        public string? CpfHash { get; init; }

        public DateTime? AnonimizadoEm { get; init; }

        public string? CpfOriginal { get; init; }
    }
}
