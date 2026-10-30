using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Testing;

namespace Funeraria.Modules.Funeral.IntegrationTests;

/// <summary>FUN-09: pedidos aos fornecedores, aceite e recusa pelo portal (TER-02), reenvio e cancelamento.</summary>
[Collection(ApiCollection.Nome)]
public sealed class PedidosTests(FuneralApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Recusado_vai_para_outro_fornecedor_que_aceita_prepara_e_entrega()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        HttpClient equipe = funeraria.Equipe();
        FornecedorDeTeste floricultura = await factory.CriarFornecedorAsync(funeraria, "Floricultura Primavera");
        FornecedorDeTeste outra = await factory.CriarFornecedorAsync(funeraria, "Flores do Campo");
        Guid coroa = await ItemDoFornecedorAsync(equipe, "Coroa de rosas", floricultura.IdTerceiro);
        Guid velas = await funeraria.CriarItemCatalogoAsync("Velas", 40m);
        FuneralDeTeste funeral = await factory.CriarFuneralAsync(funeraria);
        await AdicionarAsync(equipe, funeral.IdFuneral, coroa, 2);
        await AdicionarAsync(equipe, funeral.IdFuneral, velas, 1);
        (_, string emailGestor) = await factory.CriarUsuarioAsync((funeraria.Tenant, PerfilVinculo.Funeraria));
        HttpClient gestor = factory.Http(funeraria.Slug, await factory.EntrarAsync(funeraria.Slug, emailGestor));

        // Só o item com fornecedor vira pedido; gerar de novo não duplica.
        JsonObject geracao = await Cenarios.LerAsync(await GerarAsync(equipe, funeral.IdFuneral), HttpStatusCode.OK);
        JsonNode pedido = Assert.Single(geracao["pedidos"]!.AsArray())!;
        Assert.Equal(floricultura.IdTerceiro, pedido["id_terceiro"]!.GetValue<Guid>());
        Assert.Equal("Coroa de rosas", Assert.Single(pedido["itens"]!.AsArray())!["nome"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Conflict, (await GerarAsync(equipe, funeral.IdFuneral)).StatusCode);
        Guid idPedido = pedido["id_pedido"]!.GetValue<Guid>();

        // O fornecedor vê o pedido no portal, sem CPF nem contrato; o outro não vê nem mexe.
        JsonNode noPortal = Assert.Single((await floricultura.Http.GetFromJsonAsync<JsonArray>("/api/v1/terceiros/pedidos", Json, Ct))!)!;
        Assert.Equal("João da Silva", noPortal["nome_falecido"]!.GetValue<string>());
        Assert.DoesNotContain("cpf", noPortal.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty((await outra.Http.GetFromJsonAsync<JsonArray>("/api/v1/terceiros/pedidos", Json, Ct))!);
        Assert.Equal(HttpStatusCode.NotFound, (await AtualizarAsync(outra.Http, idPedido, "EM_PREPARO")).StatusCode);

        // Recusa pelo portal (TER-02): o pedido fica RECUSADO e a funerária manda para outro fornecedor.
        Guid atribuicao = await AtribuicaoDoPedidoAsync(floricultura.Http, idPedido);
        await floricultura.Http.PostAsJsonAsync($"/api/v1/terceiros/atribuicoes/{atribuicao}/recusa", new { motivo = "Sem rosas hoje." }, Json, Ct);
        JsonNode recusado = await StatusDoPedidoAsync(equipe, funeral.IdFuneral, idPedido, "RECUSADO");
        Assert.Equal("Sem rosas hoje.", recusado["motivo_recusa"]!.GetValue<string>());

        JsonObject reenviado = await Cenarios.LerAsync(await equipe.PostAsJsonAsync(
            $"/api/v1/funerais/{funeral.IdFuneral}/pedidos/{idPedido}/reenviar", new { id_terceiro = outra.IdTerceiro }, Json, Ct), HttpStatusCode.OK);
        Guid novoPedido = reenviado["id_pedido"]!.GetValue<Guid>();

        // Aceite pelo portal; preparo e entrega pelo endpoint do pedido. A funerária é avisada.
        Assert.Equal(HttpStatusCode.BadRequest, (await AtualizarAsync(outra.Http, novoPedido, "EM_PREPARO")).StatusCode);
        await outra.Http.PostAsync($"/api/v1/terceiros/atribuicoes/{await AtribuicaoDoPedidoAsync(outra.Http, novoPedido)}/aceite", null, Ct);
        await StatusDoPedidoAsync(equipe, funeral.IdFuneral, novoPedido, "ACEITO");
        Assert.Equal(HttpStatusCode.OK, (await AtualizarAsync(outra.Http, novoPedido, "EM_PREPARO", "Montando as coroas.")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AtualizarAsync(outra.Http, novoPedido, "CANCELADO")).StatusCode);
        JsonObject entregue = await Cenarios.LerAsync(await AtualizarAsync(outra.Http, novoPedido, "ENTREGUE"), HttpStatusCode.OK);
        Assert.Equal("ENTREGUE", entregue["status"]!.GetValue<string>());

        await CenariosFuneral.AguardarAsync(
            () => gestor.GetFromJsonAsync<JsonObject>("/api/v1/notificacoes", Json, Ct)!,
            n => n!["itens"]!.AsArray().Any(i => i!["tipo"]!.GetValue<string>() == "PEDIDO_ENTREGUE"),
            "aviso de entrega para o gestor");
    }

    [Fact]
    public async Task Funeral_cancelado_cancela_os_pedidos_abertos_e_avisa_o_fornecedor()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        HttpClient equipe = funeraria.Equipe();
        FornecedorDeTeste floricultura = await factory.CriarFornecedorAsync(funeraria, "Floricultura Primavera");
        Guid coroa = await ItemDoFornecedorAsync(equipe, "Coroa de lírios", floricultura.IdTerceiro);
        FuneralDeTeste funeral = await factory.CriarFuneralAsync(funeraria);
        await AdicionarAsync(equipe, funeral.IdFuneral, coroa, 1);
        Guid idPedido = (await Cenarios.LerAsync(await GerarAsync(equipe, funeral.IdFuneral), HttpStatusCode.OK))["pedidos"]![0]!["id_pedido"]!.GetValue<Guid>();

        await equipe.PatchAsJsonAsync($"/api/v1/funerais/{funeral.IdFuneral}/status", new { status = "CANCELADO" }, Json, Ct);

        await StatusDoPedidoAsync(equipe, funeral.IdFuneral, idPedido, "CANCELADO");
        await CenariosFuneral.AguardarAsync(
            () => floricultura.Http.GetFromJsonAsync<JsonObject>("/api/v1/notificacoes", Json, Ct)!,
            n => n!["itens"]!.AsArray().Any(i => i!["tipo"]!.GetValue<string>() == "PEDIDO_CANCELADO"),
            "aviso de cancelamento para o fornecedor");
        Assert.Equal(HttpStatusCode.Conflict, (await GerarAsync(equipe, funeral.IdFuneral)).StatusCode);
    }

    private static async Task<Guid> ItemDoFornecedorAsync(HttpClient equipe, string nome, Guid idFornecedor)
    {
        HttpResponseMessage categoria = await equipe.PostAsJsonAsync("/api/v1/gestao/catalogo/categorias", new { nome = $"Flores {Guid.NewGuid():N}"[..14] }, Json, Ct);
        Guid idCategoria = (await Cenarios.LerAsync(categoria, HttpStatusCode.Created))["id_categoria"]!.GetValue<Guid>();
        HttpResponseMessage item = await equipe.PostAsJsonAsync("/api/v1/gestao/catalogo",
            new { id_categoria = idCategoria, nome, preco = 350m, id_terceiro_fornecedor = idFornecedor }, Json, Ct);
        return (await Cenarios.LerAsync(item, HttpStatusCode.Created))["id_item"]!.GetValue<Guid>();
    }

    private static async Task AdicionarAsync(HttpClient equipe, Guid idFuneral, Guid item, int quantidade) =>
        await Cenarios.LerAsync(
            await equipe.PostAsJsonAsync($"/api/v1/funerais/{idFuneral}/itens", new { id_item_catalogo = item, quantidade }, Json, Ct),
            HttpStatusCode.OK);

    private static Task<HttpResponseMessage> GerarAsync(HttpClient equipe, Guid idFuneral) =>
        equipe.PostAsJsonAsync($"/api/v1/funerais/{idFuneral}/pedidos",
            new { local_entrega = "Sala 1 - Velório Central", data_hora_entrega = DateTime.UtcNow.AddHours(6) }, Json, Ct);

    private static Task<HttpResponseMessage> AtualizarAsync(HttpClient fornecedor, Guid idPedido, string status, string? observacao = null) =>
        fornecedor.PatchAsJsonAsync($"/api/v1/terceiros/pedidos/{idPedido}/status", new { status, observacao }, Json, Ct);

    private static async Task<Guid> AtribuicaoDoPedidoAsync(HttpClient fornecedor, Guid idPedido) =>
        (await fornecedor.GetFromJsonAsync<JsonArray>("/api/v1/terceiros/atribuicoes", Json, Ct))!
            .Single(a => a!["id_origem"]!.GetValue<Guid>() == idPedido)!["id_atribuicao"]!.GetValue<Guid>();

    /// <summary>Aceite e recusa chegam pelo outbox (eventos da atribuição).</summary>
    private static Task<JsonNode> StatusDoPedidoAsync(HttpClient equipe, Guid idFuneral, Guid idPedido, string status) =>
        CenariosFuneral.AguardarAsync(
            async () => (await equipe.GetFromJsonAsync<JsonArray>($"/api/v1/funerais/{idFuneral}/pedidos", Json, Ct))!
                .Single(p => p!["id_pedido"]!.GetValue<Guid>() == idPedido)!,
            p => p["status"]!.GetValue<string>() == status,
            $"pedido {status}");
}
