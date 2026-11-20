using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Amazon.S3;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Lgpd;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Funeral.IntegrationTests;

/// <summary>
/// SEG-01: política de retenção das preferências de funeral e dos dados do falecido. As datas são empurradas para o
/// passado direto no banco; a regra roda como o job a rodaria. Cada teste confere só os próprios registros.
/// </summary>
[Collection(ApiCollection.Nome)]
public sealed class RetencaoLgpdTests(FuneralApiFactory factory)
{
    private static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n"u8.ToArray();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preferencias_saem_quando_o_ultimo_contrato_termina_e_ficam_enquanto_ha_contrato()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste encerrado = await funeraria.CriarClienteAsync(dependentes: 1);
        Guid contrato = await encerrado.ContratarAsync(plano);
        ClienteDeTeste vigente = await funeraria.CriarClienteAsync();
        await vigente.ContratarAsync(plano);
        await SalvarPreferenciaAsync(encerrado, encerrado.IdCliente);
        await SalvarPreferenciaAsync(encerrado, encerrado.Dependentes[0]);
        await SalvarPreferenciaAsync(vigente, vigente.IdCliente);
        await ExecutarAsync(
            "UPDATE contratos.contratos SET status = 'Cancelado', data_fim = current_date - interval '60 days' WHERE id_contrato = @contrato",
            new { contrato });

        int simulados = await AplicarAsync("preferencias-de-funeral", simulacao: true);
        Assert.True(simulados >= 2);
        Assert.Equal(2, await ContarPreferenciasAsync(encerrado.IdCliente));

        await AplicarAsync("preferencias-de-funeral", simulacao: false);

        Assert.Equal(0, await ContarPreferenciasAsync(encerrado.IdCliente));
        Assert.Equal(1, await ContarPreferenciasAsync(vigente.IdCliente));
    }

    [Fact]
    public async Task Falecido_cinco_anos_apos_o_funeral_e_anonimizado_e_os_arquivos_do_obito_sao_excluidos()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        FuneralDeTeste funeral = await factory.CriarFuneralAsync(funeraria);
        HttpResponseMessage anexado = await DocumentosTests.EnviarAsync(funeraria.Equipe(Perfis.Funcionario),
            $"/api/v1/beneficiarios/{funeral.IdBeneficiario}/documentos", Pdf, "declaracao.pdf", "DECLARACAO_OBITO");
        Guid idDocumento = (await Cenarios.LerAsync(anexado, HttpStatusCode.Created))["id_documento"]!.GetValue<Guid>();
        string chave = await ConsultarAsync<string>("SELECT chave FROM documentos.documentos WHERE id_documento = @id", new { id = idDocumento });
        await EncerrarHaAnosAsync(funeral, anos: 6);

        Assert.True(await AplicarAsync("obitos", simulacao: true) >= 1);
        Assert.Equal("Hospital Central", await LocalDoFalecimentoAsync(funeral.IdBeneficiario));

        await AplicarAsync("obitos", simulacao: false);

        Assert.Equal("Anonimizado", await LocalDoFalecimentoAsync(funeral.IdBeneficiario));
        Assert.Equal("Pessoa anonimizada", await ConsultarAsync<string>(
            "SELECT responsavel_nome FROM funeral.beneficiarios WHERE id_beneficiario = @id", new { id = funeral.IdBeneficiario }));
        Assert.Equal("Pessoa anonimizada", await ConsultarAsync<string>(
            "SELECT nome_falecido FROM funeral.funerais WHERE id_funeral = @id", new { id = funeral.IdFuneral }));

        // Pelo outbox: Documentos exclui o arquivo e a linha do documento.
        await CenariosFuneral.AguardarAsync(
            () => ConsultarAsync<long>("SELECT count(*) FROM documentos.documentos WHERE id_documento = @id", new { id = idDocumento }),
            total => total == 0,
            "documento do óbito excluído");
        AmazonS3Exception ausente = await Assert.ThrowsAsync<AmazonS3Exception>(() =>
            factory.Armazenamento!.CriarCliente().GetObjectMetadataAsync(ArmazenamentoDeTeste.Bucket, chave, Ct));
        Assert.Equal(HttpStatusCode.NotFound, ausente.StatusCode);
    }

    [Fact]
    public async Task Funeral_recente_ou_em_aberto_segura_o_falecido()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        FuneralDeTeste recente = await factory.CriarFuneralAsync(funeraria);
        await EncerrarHaAnosAsync(recente, anos: 2);
        FuneralDeTeste aberto = await factory.CriarFuneralAsync(funeraria);
        await ExecutarAsync(
            """
            UPDATE funeral.beneficiarios SET data_falecimento = current_date - interval '6 years' WHERE id_beneficiario = @beneficiario;
            UPDATE funeral.funerais SET data_hora_prevista = now() - interval '6 years' WHERE id_funeral = @funeral;
            """,
            new { beneficiario = aberto.IdBeneficiario, funeral = aberto.IdFuneral });

        await AplicarAsync("obitos", simulacao: false);

        Assert.Equal("Hospital Central", await LocalDoFalecimentoAsync(recente.IdBeneficiario));
        Assert.Equal("Hospital Central", await LocalDoFalecimentoAsync(aberto.IdBeneficiario));
    }

    private Task EncerrarHaAnosAsync(FuneralDeTeste funeral, int anos) =>
        ExecutarAsync(
            $"""
            UPDATE funeral.beneficiarios SET data_falecimento = current_date - interval '{anos} years' WHERE id_beneficiario = @beneficiario;
            UPDATE funeral.funerais SET status = 'Concluido', data_hora_prevista = now() - interval '{anos} years' WHERE id_funeral = @funeral;
            """,
            new { beneficiario = funeral.IdBeneficiario, funeral = funeral.IdFuneral });

    private async Task SalvarPreferenciaAsync(ClienteDeTeste cliente, Guid idPessoa)
    {
        HttpResponseMessage salvo = await cliente.Http.PutAsJsonAsync($"/api/v1/preferencias-funeral/{idPessoa}",
            new { destinacao = "SEPULTAMENTO", crenca_religiosa = "Católica" }, JsonApi.Opcoes, Ct);
        Assert.True(salvo.IsSuccessStatusCode, await salvo.Content.ReadAsStringAsync(Ct));
    }

    private Task<long> ContarPreferenciasAsync(Guid idCliente) =>
        ConsultarAsync<long>("SELECT count(*) FROM funeral.preferencias WHERE id_cliente = @idCliente", new { idCliente });

    private Task<string> LocalDoFalecimentoAsync(Guid idBeneficiario) =>
        ConsultarAsync<string>("SELECT local_falecimento FROM funeral.beneficiarios WHERE id_beneficiario = @idBeneficiario", new { idBeneficiario });

    private async Task<int> AplicarAsync(string nome, bool simulacao)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IPoliticaRetencao politica = scope.ServiceProvider.GetServices<IPoliticaRetencao>().Single(p => p.Nome == nome);
        return await politica.AplicarAsync(new ExecucaoRetencao(DateTime.UtcNow, simulacao, new RetencaoOptions()), Ct);
    }

    private async Task<T> ConsultarAsync<T>(string sql, object parametros)
    {
        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        return await conexao.QuerySingleAsync<T>(new CommandDefinition(sql, parametros, cancellationToken: Ct));
    }

    private async Task ExecutarAsync(string sql, object parametros)
    {
        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        await conexao.ExecuteAsync(new CommandDefinition(sql, parametros, cancellationToken: Ct));
    }
}
