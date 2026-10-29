using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Testing;

namespace Funeraria.Modules.Funeral.IntegrationTests;

/// <summary>Gerar Beneficiário, Registrar Funeral, etapas (FUN-06) e acompanhamento (FUN-07).</summary>
[Collection(ApiCollection.Nome)]
public sealed class FuneraisTests(FuneralApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Now);

    [Fact]
    public async Task Beneficiario_exige_elegibilidade_e_e_registrado_uma_vez()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        ClienteDeTeste emCarencia = await funeraria.CriarClienteAsync();
        Guid idContratoEmCarencia = await emCarencia.ContratarAsync(await funeraria.CriarPlanoAsync(carenciaDias: 90));
        await factory.AtivarContratoAsync(idContratoEmCarencia);
        HttpClient equipe = funeraria.Equipe(Perfis.Funcionario);

        HttpResponseMessage naoElegivel = await RegistrarBeneficiarioAsync(equipe, await TitularAsync(funeraria, idContratoEmCarencia), Hoje);
        Assert.Contains("carência", (await Cenarios.LerAsync(naoElegivel, HttpStatusCode.Conflict))["detail"]!.GetValue<string>(), StringComparison.Ordinal);

        (ClienteDeTeste cliente, Guid idContrato) = await ContratoAtivoAsync(funeraria);
        Guid titular = await TitularAsync(funeraria, idContrato);
        Assert.Equal(HttpStatusCode.BadRequest, (await RegistrarBeneficiarioAsync(equipe, titular, Hoje.AddDays(1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RegistrarBeneficiarioAsync(funeraria.Equipe(Perfis.Consultor), titular, Hoje)).StatusCode);

        JsonObject beneficiario = await Cenarios.LerAsync(await RegistrarBeneficiarioAsync(equipe, titular, Hoje), HttpStatusCode.Created);
        Assert.Equal("João da Silva", beneficiario["nome"]!.GetValue<string>());
        Assert.Equal("Ana Silva", beneficiario["responsavel"]!["nome"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Conflict, (await RegistrarBeneficiarioAsync(equipe, titular, Hoje)).StatusCode);

        Guid id = beneficiario["id_beneficiario"]!.GetValue<Guid>();
        Assert.Equal(HttpStatusCode.OK, (await cliente.Http.GetAsync($"/api/v1/beneficiarios/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await emCarencia.Http.GetAsync($"/api/v1/beneficiarios/{id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Funeral_segue_as_etapas_avisa_a_familia_e_trava_o_contrato()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        (ClienteDeTeste cliente, Guid idContrato) = await ContratoAtivoAsync(funeraria, dependentes: 1);
        HttpClient equipe = funeraria.Equipe();
        JsonObject contrato = (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/contratos/{idContrato}", Json, Ct))!;
        Guid pessoaDependente = contrato["pessoas_cobertas"]!.AsArray()
            .Single(p => p!["tipo"]!.GetValue<string>() == "DEPENDENTE")!["id_pessoa_coberta"]!.GetValue<Guid>();
        Guid idBeneficiario = (await Cenarios.LerAsync(
            await RegistrarBeneficiarioAsync(equipe, pessoaDependente, Hoje.AddDays(-1)), HttpStatusCode.Created))["id_beneficiario"]!.GetValue<Guid>();

        DateTime antesDoFalecimento = DateTime.UtcNow.AddDays(-3);
        Assert.Equal(HttpStatusCode.BadRequest, (await RegistrarFuneralAsync(equipe, idBeneficiario, antesDoFalecimento)).StatusCode);

        JsonObject funeral = await Cenarios.LerAsync(await RegistrarFuneralAsync(equipe, idBeneficiario, DateTime.UtcNow.AddDays(1)), HttpStatusCode.Created);
        Guid idFuneral = funeral["id_funeral"]!.GetValue<Guid>();
        Assert.Equal("REGISTRADO", funeral["status"]!.GetValue<string>());
        Assert.Equal(["EM_PREPARACAO", "CANCELADO"], funeral["proximos_status"]!.AsArray().Select(s => s!.GetValue<string>()).Order().Reverse());
        Assert.Equal(HttpStatusCode.Conflict, (await RegistrarFuneralAsync(equipe, idBeneficiario, DateTime.UtcNow.AddDays(1))).StatusCode);

        // Pular etapa é proibido; avançar entra no histórico e avisa o titular.
        Assert.Equal(HttpStatusCode.BadRequest, (await MudarStatusAsync(equipe, idFuneral, "VELORIO")).StatusCode);
        JsonObject preparando = await Cenarios.LerAsync(await MudarStatusAsync(equipe, idFuneral, "EM_PREPARACAO", "Corpo recebido."), HttpStatusCode.OK);
        JsonNode ultimo = preparando["historico"]!.AsArray().Last()!;
        Assert.Equal("EM_PREPARACAO", ultimo["para"]!.GetValue<string>());
        Assert.Equal("Corpo recebido.", ultimo["observacao"]!.GetValue<string>());
        await AguardarNotificacaoAsync(cliente.Http, "FUNERAL_STATUS");

        // Com o funeral em andamento: o contrato não é cancelado e o falecido não sai do contrato (CON-02, CON-03).
        HttpResponseMessage cancelar = await equipe.PostAsJsonAsync($"/api/v1/contratos/{idContrato}/cancelar", new { motivo = "Teste." }, Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, cancelar.StatusCode);
        HttpResponseMessage remover = await equipe.DeleteAsync($"/api/v1/contratos/{idContrato}/dependentes/{cliente.Dependentes[0]}", Ct);
        Assert.Equal(HttpStatusCode.Conflict, remover.StatusCode);

        // Acompanhamento: a equipe lista, o titular vê o próprio, outro cliente não vê.
        JsonObject lista = (await equipe.GetFromJsonAsync<JsonObject>("/api/v1/gestao/funerais?status=EM_PREPARACAO", Json, Ct))!;
        Assert.Equal(idFuneral, Assert.Single(lista["itens"]!.AsArray())!["id_funeral"]!.GetValue<Guid>());
        Assert.Equal(0, (await equipe.GetFromJsonAsync<JsonObject>("/api/v1/gestao/funerais?status=VELORIO", Json, Ct))!["total"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.OK, (await cliente.Http.GetAsync($"/api/v1/funerais/{idFuneral}", Ct)).StatusCode);
        ClienteDeTeste outro = await funeraria.CriarClienteAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Http.GetAsync($"/api/v1/funerais/{idFuneral}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await MudarStatusAsync(cliente.Http, idFuneral, "VELORIO")).StatusCode);

        await Cenarios.LerAsync(await MudarStatusAsync(equipe, idFuneral, "VELORIO"), HttpStatusCode.OK);
        JsonObject concluido = await Cenarios.LerAsync(await MudarStatusAsync(equipe, idFuneral, "CONCLUIDO"), HttpStatusCode.OK);
        Assert.Empty(concluido["proximos_status"]!.AsArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await MudarStatusAsync(equipe, idFuneral, "CANCELADO")).StatusCode);

        // Concluído, o contrato já pode ser cancelado.
        cancelar = await equipe.PostAsJsonAsync($"/api/v1/contratos/{idContrato}/cancelar", new { motivo = "Família encerrou o plano." }, Json, Ct);
        Assert.Equal(HttpStatusCode.OK, cancelar.StatusCode);
    }

    private async Task<(ClienteDeTeste Cliente, Guid IdContrato)> ContratoAtivoAsync(FunerariaDeTeste funeraria, int dependentes = 0)
    {
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes);
        Guid idContrato = await cliente.ContratarAsync(await funeraria.CriarPlanoAsync(carenciaDias: 0));
        await factory.AtivarContratoAsync(idContrato);
        return (cliente, idContrato);
    }

    private static async Task<Guid> TitularAsync(FunerariaDeTeste funeraria, Guid idContrato) =>
        (await funeraria.Equipe().GetFromJsonAsync<JsonObject>($"/api/v1/contratos/{idContrato}", Json, Ct))!["pessoas_cobertas"]!.AsArray()
            .Single(p => p!["tipo"]!.GetValue<string>() == "TITULAR")!["id_pessoa_coberta"]!.GetValue<Guid>();

    private static Task<HttpResponseMessage> RegistrarBeneficiarioAsync(HttpClient equipe, Guid idPessoaCoberta, DateOnly dataFalecimento) =>
        equipe.PostAsJsonAsync("/api/v1/beneficiarios", new
        {
            id_pessoa_coberta = idPessoaCoberta,
            data_falecimento = dataFalecimento,
            local_falecimento = "Hospital Central",
            responsavel = new { nome = "Ana Silva", telefone = "(44) 98888-7777", parentesco = "Filha" }
        }, Json, Ct);

    private static Task<HttpResponseMessage> RegistrarFuneralAsync(HttpClient equipe, Guid idBeneficiario, DateTime dataHora) =>
        equipe.PostAsJsonAsync("/api/v1/funerais", new
        {
            id_beneficiario = idBeneficiario,
            data_hora_prevista = dataHora,
            destinacao = "SEPULTAMENTO"
        }, Json, Ct);

    private static Task<HttpResponseMessage> MudarStatusAsync(HttpClient http, Guid idFuneral, string status, string? observacao = null) =>
        http.PatchAsJsonAsync($"/api/v1/funerais/{idFuneral}/status", new { status, observacao }, Json, Ct);

    /// <summary>A notificação nasce pelo outbox.</summary>
    private static async Task AguardarNotificacaoAsync(HttpClient http, string tipo)
    {
        for (int i = 0; i < 75; i++)
        {
            JsonObject central = (await http.GetFromJsonAsync<JsonObject>("/api/v1/notificacoes", Json, Ct))!;
            if (central["itens"]!.AsArray().Any(n => n!["tipo"]!.GetValue<string>() == tipo))
            {
                return;
            }

            await Task.Delay(200, Ct);
        }

        throw new TimeoutException($"Nenhuma notificação {tipo}.");
    }
}
