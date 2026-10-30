using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Modules.Contratos.Contracts.IntegrationEvents;
using Funeraria.Testing;
using static Funeraria.Modules.Contratos.IntegrationTests.ContratosApiFactory;

namespace Funeraria.Modules.Contratos.IntegrationTests;

/// <summary>CON-02 (dependentes depois da contratação) e CON-03 (cancelamento).</summary>
[Collection(ApiCollection.Nome)]
public sealed class AlteracoesContratoTests(ContratosApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Inclui_e_remove_dependente_recalculando_o_valor_e_respeitando_o_limite()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync(limiteDependentes: 1);
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 2);
        Guid idContrato = await SoTitularAsync(cliente, plano);

        HttpResponseMessage incluido = await IncluirAsync(cliente.Http, idContrato, cliente.Dependentes[0]);
        JsonObject contrato = await Cenarios.LerAsync(incluido, HttpStatusCode.OK);
        Assert.Equal("TITULAR_E_DEPENDENTES", contrato["tipo_cobertura"]!.GetValue<string>());
        Assert.Equal(109.80m, contrato["valor_total"]!.GetValue<decimal>());
        JsonNode dependente = contrato["pessoas_cobertas"]!.AsArray().Single(p => p!["tipo"]!.GetValue<string>() == "DEPENDENTE")!;
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), DateOnly.Parse(dependente["data_inclusao"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture));

        PessoasDoContratoAlteradasIntegrationEvent evento = await factory.Eventos
            .AguardarAsync<PessoasDoContratoAlteradasIntegrationEvent>(e => e.IdContrato == idContrato);
        Assert.Equal(1, evento.QuantidadeDependentes);

        Assert.Equal(HttpStatusCode.Conflict, (await IncluirAsync(cliente.Http, idContrato, cliente.Dependentes[0])).StatusCode);
        HttpResponseMessage acimaDoLimite = await IncluirAsync(cliente.Http, idContrato, cliente.Dependentes[1]);
        Assert.Contains("limite", (await Cenarios.LerAsync(acimaDoLimite, HttpStatusCode.BadRequest))["detail"]!.GetValue<string>(), StringComparison.Ordinal);

        ClienteDeTeste outro = await funeraria.CriarClienteAsync(dependentes: 1);
        Assert.Equal(HttpStatusCode.BadRequest, (await IncluirAsync(funeraria.Equipe(), idContrato, outro.Dependentes[0])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await IncluirAsync(outro.Http, idContrato, outro.Dependentes[0])).StatusCode);

        HttpResponseMessage removido = await cliente.Http.DeleteAsync($"/api/v1/contratos/{idContrato}/dependentes/{cliente.Dependentes[0]}", Ct);
        JsonObject depois = await Cenarios.LerAsync(removido, HttpStatusCode.OK);
        Assert.Equal("TITULAR", depois["tipo_cobertura"]!.GetValue<string>());
        Assert.Equal(89.90m, depois["valor_total"]!.GetValue<decimal>());
        Assert.Single(depois["pessoas_cobertas"]!.AsArray());
        Assert.Equal(HttpStatusCode.NotFound,
            (await cliente.Http.DeleteAsync($"/api/v1/contratos/{idContrato}/dependentes/{cliente.Dependentes[0]}", Ct)).StatusCode);

        // Depois de sair, o dependente pode voltar (com carência nova) ou ir para outro contrato.
        Assert.Equal(HttpStatusCode.OK, (await IncluirAsync(funeraria.Equipe(Perfis.Funcionario), idContrato, cliente.Dependentes[1])).StatusCode);
    }

    [Fact]
    public async Task Cancelamento_guarda_o_motivo_e_o_historico_e_libera_os_dependentes()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);
        Guid idContrato = await cliente.ContratarAsync(plano);
        await factory.AtivarContratoAsync(idContrato);

        HttpResponseMessage semMotivo = await Cancelar(cliente.Http, idContrato, "");
        Assert.Equal(HttpStatusCode.BadRequest, semMotivo.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Cancelar(funeraria.Equipe(Perfis.Consultor), idContrato, "Pedido da família.")).StatusCode);
        ClienteDeTeste outro = await funeraria.CriarClienteAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await Cancelar(outro.Http, idContrato, "Não é meu.")).StatusCode);

        JsonObject cancelado = await Cenarios.LerAsync(await Cancelar(cliente.Http, idContrato, "Mudança de cidade."), HttpStatusCode.OK);
        Assert.Equal("CANCELADO", cancelado["status"]!.GetValue<string>());
        Assert.Equal("Mudança de cidade.", cancelado["motivo_cancelamento"]!.GetValue<string>());
        Assert.NotNull(cancelado["data_fim"]);
        Assert.Equal(2, cancelado["pessoas_cobertas"]!.AsArray().Count);

        ContratoCanceladoIntegrationEvent evento = await factory.Eventos.AguardarAsync<ContratoCanceladoIntegrationEvent>(e => e.IdContrato == idContrato);
        Assert.Equal("Mudança de cidade.", evento.Motivo);

        Assert.Equal(HttpStatusCode.Conflict, (await Cancelar(funeraria.Equipe(), idContrato, "De novo.")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await IncluirAsync(cliente.Http, idContrato, cliente.Dependentes[0])).StatusCode);

        JsonObject detalhe = (await funeraria.Equipe().GetFromJsonAsync<JsonObject>($"/api/v1/contratos/{idContrato}", Json, Ct))!;
        Assert.Equal(2, detalhe["pessoas_cobertas"]!.AsArray().Count);
        Assert.Equal("Mudança de cidade.", detalhe["motivo_cancelamento"]!.GetValue<string>());

        // Titular e dependente podem contratar de novo, até no mesmo plano.
        Assert.NotEqual(idContrato, await cliente.ContratarAsync(plano));
    }

    private static async Task<Guid> SoTitularAsync(ClienteDeTeste cliente, Guid plano)
    {
        HttpResponseMessage response = await cliente.Http.PostAsJsonAsync("/api/v1/contratos", new
        {
            id_plano = plano,
            tipo_cobertura = "TITULAR",
            periodicidade = "MENSAL"
        }, Json, Ct);
        return (await Cenarios.LerAsync(response, HttpStatusCode.Created))["id_contrato"]!.GetValue<Guid>();
    }

    private static Task<HttpResponseMessage> IncluirAsync(HttpClient http, Guid idContrato, Guid idDependente) =>
        http.PostAsJsonAsync($"/api/v1/contratos/{idContrato}/dependentes", new { id_dependente = idDependente }, Json, Ct);

    private static Task<HttpResponseMessage> Cancelar(HttpClient http, Guid idContrato, string motivo) =>
        http.PostAsJsonAsync($"/api/v1/contratos/{idContrato}/cancelar", new { motivo }, Json, Ct);
}
