using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.Modules.Documentos.Application.Certidoes;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Funeral.IntegrationTests;

/// <summary>CER-02 a CER-04: certidão de óbito no modo manual (com despachante), no simulado e o alerta de prazo.</summary>
[Collection(ApiCollection.Nome)]
public sealed class CertidoesTests(FuneralApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n"u8.ToArray();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Despachante_conduz_a_certidao_e_o_titular_e_avisado_quando_sai()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        FuneralDeTeste funeral = await factory.CriarFuneralAsync(funeraria);
        HttpClient equipe = funeraria.Equipe();
        string rota = $"/api/v1/beneficiarios/{funeral.IdBeneficiario}";

        // Sem os documentos obrigatórios, não pede; o checklist mostra o que falta e o prazo legal.
        HttpResponseMessage semDocumentos = await equipe.PostAsync($"{rota}/certidao", null, Ct);
        Assert.Contains("Declaração de óbito", (await Cenarios.LerAsync(semDocumentos, HttpStatusCode.Conflict))["detail"]!.GetValue<string>(), StringComparison.Ordinal);
        JsonObject checklist = (await funeral.Cliente.Http.GetFromJsonAsync<JsonObject>($"{rota}/certidao/checklist", Json, Ct))!;
        Assert.False(checklist["pronto_para_solicitar"]!.GetValue<bool>());
        Assert.Equal(15, checklist["prazo"]!["dias_restantes"]!.GetValue<int>());

        await AnexarObrigatoriosAsync(equipe, rota);
        JsonObject solicitada = await Cenarios.LerAsync(await equipe.PostAsync($"{rota}/certidao", null, Ct), HttpStatusCode.Created);
        Assert.Equal("SOLICITADA", solicitada["status"]!.GetValue<string>());
        Assert.Equal("MANUAL", solicitada["modo"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Conflict, (await equipe.PostAsync($"{rota}/certidao", null, Ct)).StatusCode);
        Guid idSolicitacao = solicitada["id_solicitacao"]!.GetValue<Guid>();

        // O despachante recebe só os dados e documentos desta solicitação.
        FornecedorDeTeste despachante = await factory.CriarTerceiroAsync(funeraria, "Despachante Ágil", "CARTORIO_DESPACHANTE");
        FornecedorDeTeste outro = await factory.CriarTerceiroAsync(funeraria, "Outro Despachante", "CARTORIO_DESPACHANTE");
        JsonObject atribuida = await Cenarios.LerAsync(await equipe.PatchAsJsonAsync($"{rota}/certidao/responsavel",
            new { tipo = "TERCEIRO", id_terceiro = despachante.IdTerceiro }, Json, Ct), HttpStatusCode.OK);
        Assert.Equal("Despachante Ágil", atribuida["responsavel"]!["nome"]!.GetValue<string>());

        JsonNode atribuicao = (await despachante.Http.GetFromJsonAsync<JsonArray>("/api/v1/terceiros/atribuicoes", Json, Ct))!
            .Single(a => a!["id_origem"]!.GetValue<Guid>() == idSolicitacao)!;
        JsonArray docs = atribuicao["resumo"]!["documentos"]!.AsArray();
        Assert.Equal(3, docs.Count);
        Assert.DoesNotContain("cpf", atribuicao.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Guid umDocumento = docs[0]!["id_documento"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.OK, (await despachante.Http.GetAsync($"/api/v1/terceiros/certidoes/{idSolicitacao}/documentos/{umDocumento}/download", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Http.GetAsync($"/api/v1/terceiros/certidoes/{idSolicitacao}/documentos/{umDocumento}/download", Ct)).StatusCode);

        // Etapas pelo portal: emitida só com o PDF e a matrícula.
        string portal = $"/api/v1/terceiros/certidoes/{idSolicitacao}";
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Http.PatchAsJsonAsync($"{portal}/status", new { status = "EM_PROCESSAMENTO" }, Json, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await despachante.Http.PatchAsJsonAsync($"{portal}/status", new { status = "REJEITADA" }, Json, Ct)).StatusCode);
        JsonObject enviada = await Cenarios.LerAsync(await despachante.Http.PatchAsJsonAsync($"{portal}/status",
            new { status = "EM_PROCESSAMENTO", protocolo = "CRC-2026-001" }, Json, Ct), HttpStatusCode.OK);
        Assert.Equal("CRC-2026-001", enviada["protocolo"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await despachante.Http.PatchAsJsonAsync($"{portal}/status", new { status = "EMITIDA" }, Json, Ct)).StatusCode);

        JsonObject emitida = await Cenarios.LerAsync(await EnviarEmitidaAsync(despachante.Http, $"{portal}/emitida", "123456 01 55 2026 4 00012 345 0001234-56"), HttpStatusCode.OK);
        Assert.Equal("EMITIDA", emitida["status"]!.GetValue<string>());
        Assert.NotNull(emitida["id_documento_certidao"]);

        // O titular é avisado e vê a certidão nos documentos.
        await CenariosFuneral.AguardarAsync(
            () => funeral.Cliente.Http.GetFromJsonAsync<JsonObject>("/api/v1/notificacoes", Json, Ct)!,
            n => n!["itens"]!.AsArray().Any(i => i!["tipo"]!.GetValue<string>() == "CERTIDAO_EMITIDA"),
            "aviso da certidão para o titular");
        JsonArray documentos = (await funeral.Cliente.Http.GetFromJsonAsync<JsonArray>($"{rota}/documentos", Json, Ct))!;
        Assert.Contains(documentos, d => d!["tipo"]!.GetValue<string>() == "CERTIDAO_OBITO");
        Assert.Equal(HttpStatusCode.OK, (await funeral.Cliente.Http.GetAsync($"{rota}/certidao", Ct)).StatusCode);
    }

    [Fact]
    public async Task Modo_simulado_emite_na_hora()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        await factory.AlterarParametrosAsync(funeraria.Tenant, p => p with { Certidao = new ParametrosCertidao { Modo = ModoCertidao.Simulado } });
        FuneralDeTeste funeral = await factory.CriarFuneralAsync(funeraria);
        HttpClient equipe = funeraria.Equipe();
        string rota = $"/api/v1/beneficiarios/{funeral.IdBeneficiario}";
        await AnexarObrigatoriosAsync(equipe, rota);

        JsonObject certidao = await Cenarios.LerAsync(await equipe.PostAsync($"{rota}/certidao", null, Ct), HttpStatusCode.Created);

        Assert.Equal("SIMULADO", certidao["modo"]!.GetValue<string>());
        Assert.Equal("EMITIDA", certidao["status"]!.GetValue<string>());
        Assert.StartsWith("SIMULADA-", certidao["numero_matricula"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Alerta_avisa_os_gestores_do_prazo_vencido()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        FuneralDeTeste funeral = await factory.CriarFuneralAsync(funeraria, dataFalecimento: DateOnly.FromDateTime(DateTime.Now).AddDays(-20));
        HttpClient equipe = funeraria.Equipe();
        string rota = $"/api/v1/beneficiarios/{funeral.IdBeneficiario}";
        await AnexarObrigatoriosAsync(equipe, rota);
        JsonObject certidao = await Cenarios.LerAsync(await equipe.PostAsync($"{rota}/certidao", null, Ct), HttpStatusCode.Created);
        Assert.True(certidao["prazo"]!["vencido"]!.GetValue<bool>());
        (_, string emailGestor) = await factory.CriarUsuarioAsync((funeraria.Tenant, PerfilVinculo.Funeraria));
        HttpClient gestor = factory.Http(funeraria.Slug, await factory.EntrarAsync(funeraria.Slug, emailGestor));

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<AlertaPrazoCertidoes>().ExecutarAsync(Ct) >= 1);
        }

        JsonObject central = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/notificacoes", Json, Ct))!;
        Assert.Contains(central["itens"]!.AsArray(), n => n!["tipo"]!.GetValue<string>() == "CERTIDAO_PRAZO");
    }

    private static async Task AnexarObrigatoriosAsync(HttpClient equipe, string rota)
    {
        foreach (string tipo in new[] { "DECLARACAO_OBITO", "DOCUMENTO_FALECIDO", "DOCUMENTO_DECLARANTE" })
        {
            await Cenarios.LerAsync(await DocumentosTests.EnviarAsync(equipe, $"{rota}/documentos", Pdf, $"{tipo}.pdf", tipo), HttpStatusCode.Created);
        }
    }

    private static async Task<HttpResponseMessage> EnviarEmitidaAsync(HttpClient http, string rota, string matricula)
    {
        using var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(Pdf);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(arquivo, "arquivo", "certidao.pdf");
        form.Add(new StringContent(matricula), "numero_matricula");
        return await http.PostAsync(rota, form, Ct);
    }
}
