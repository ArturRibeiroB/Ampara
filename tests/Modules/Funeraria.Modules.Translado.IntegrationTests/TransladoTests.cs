using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Modules.Translado.Application.Translados;
using Funeraria.Modules.Translado.Domain.Translados;
using Funeraria.Modules.Translado.Infrastructure.Database;
using Funeraria.Testing;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Funeraria.Modules.Translado.IntegrationTests;

/// <summary>Distância da API, controlada pelo teste.</summary>
public sealed class DistanciaFake : IDistanciaGateway
{
    public decimal? Km { get; set; } = 120m;

    public int Chamadas { get; private set; }

    public Task<decimal?> CalcularKmAsync(Endereco origem, Endereco destino, CancellationToken cancellationToken)
    {
        Chamadas++;
        return Task.FromResult(Km);
    }
}

public sealed class TransladoApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public DistanciaFake Distancia { get; } = new();

    protected override bool UsaArmazenamento => true;

    public ValueTask InitializeAsync() => IniciarAsync();

    protected override void ConfigurarServicos(IServiceCollection services) => services.AddSingleton<IDistanciaGateway>(Distancia);
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<TransladoApiFactory>
{
    public const string Nome = "Api";
}

/// <summary>TRA-01 a TRA-06: registro, preço, documentos, empresa pelo portal, status e tempo real.</summary>
[Collection(ApiCollection.Nome)]
public sealed class TransladoTests(TransladoApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n"u8.ToArray();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly object EmMaringa = new { endereco = "Rua A, 1", cidade = "Maringá", uf = "PR" };

    [Fact]
    public async Task Tipo_e_preco_saem_da_tabela_e_dos_km_do_plano()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        HttpClient gestor = funeraria.Equipe();
        Assert.Equal(HttpStatusCode.Conflict, (await RegistrarAsync(gestor, await FuneralAsync(funeraria), EmMaringa, 10m)).StatusCode);

        // Buraco entre as faixas é recusado; funcionário não mexe na tabela.
        HttpResponseMessage comBuraco = await gestor.PutAsJsonAsync("/api/v1/gestao/translados/tabela-precos", new
        {
            faixas = new object[] { new { de_km = 0, ate_km = 50, valor = 300 }, new { de_km = 60, valor = 900, valor_km_excedente = 3 } }
        }, Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, comBuraco.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await funeraria.Equipe(Perfis.Funcionario).PutAsJsonAsync("/api/v1/gestao/translados/tabela-precos", new { faixas = TabelaValida }, Json, Ct)).StatusCode);
        await DefinirTabelaAsync(gestor);

        // Plano com 50 km incluídos.
        Guid item = await funeraria.CriarItemCatalogoAsync("Translado");
        Guid plano = await funeraria.CriarPlanoAsync(carenciaDias: 0);
        await gestor.PutAsJsonAsync($"/api/v1/gestao/planos/{plano}/coberturas",
            new { coberturas = new[] { new { id_item_catalogo = item, quantidade = 1, km_translado_incluso = 50 } } }, Json, Ct);
        FuneralCriado funeral = await FuneralAsync(funeraria, plano);

        // Dentro dos km do plano: nada a pagar. Municipal, com km informados.
        JsonObject municipal = await Cenarios.LerAsync(await RegistrarAsync(gestor, funeral, new { endereco = "Rua B, 2", cidade = "MARINGA", uf = "pr" }, 40m), HttpStatusCode.Created);
        Assert.Equal("MUNICIPAL", municipal["tipo"]!.GetValue<string>());
        Assert.True(municipal["distancia_manual"]!.GetValue<bool>());
        Assert.Equal(300m, municipal["valor"]!.GetValue<decimal>());
        Assert.Equal(0m, municipal["valor_a_pagar"]!.GetValue<decimal>());

        // Acima: paga a diferença entre o preço total (900) e o dos km incluídos (300).
        factory.Distancia.Km = 120m;
        JsonObject intermunicipal = await Cenarios.LerAsync(await RegistrarAsync(gestor, funeral, new { endereco = "Rua C, 3", cidade = "Londrina", uf = "PR" }), HttpStatusCode.Created);
        Assert.Equal("INTERMUNICIPAL", intermunicipal["tipo"]!.GetValue<string>());
        Assert.False(intermunicipal["distancia_manual"]!.GetValue<bool>());
        Assert.Equal(600m, intermunicipal["valor_a_pagar"]!.GetValue<decimal>());

        // A mesma rota não consulta a API de novo; a tabela nova não muda o que já foi registrado.
        int chamadas = factory.Distancia.Chamadas;
        JsonObject simulacao = await Cenarios.LerAsync(await gestor.PostAsJsonAsync("/api/v1/translados/simular",
            new { origem = EmMaringa, destino = new { endereco = "Rua C, 3", cidade = "Londrina", uf = "PR" } }, Json, Ct), HttpStatusCode.OK);
        Assert.Equal(900m, simulacao["valor"]!.GetValue<decimal>());
        Assert.Equal(chamadas, factory.Distancia.Chamadas);

        // Faixa aberta: 1.800 + 3 por km acima de 200.
        JsonObject longe = await Cenarios.LerAsync(await RegistrarAsync(gestor, funeral, new { endereco = "Rua D, 4", cidade = "Curitiba", uf = "SC" }, 300m), HttpStatusCode.Created);
        Assert.Equal("INTERESTADUAL", longe["tipo"]!.GetValue<string>());
        Assert.Equal(2100m, longe["valor"]!.GetValue<decimal>());

        // API sem resposta: pede os km. Internacional: recusado com a mensagem do card.
        factory.Distancia.Km = null;
        HttpResponseMessage semDistancia = await RegistrarAsync(gestor, funeral, new { endereco = "Rua E, 5", cidade = "Cascavel", uf = "PR" });
        Assert.Contains("distancia_km", (await Cenarios.LerAsync(semDistancia, HttpStatusCode.BadRequest))["detail"]!.GetValue<string>(), StringComparison.Ordinal);
        factory.Distancia.Km = 120m;
        HttpResponseMessage internacional = await RegistrarAsync(gestor, funeral, new { endereco = "Calle 1", cidade = "Assunção", uf = "AS", pais = "PY" }, 400m);
        Assert.Equal("Translado internacional ainda não está disponível.", (await Cenarios.LerAsync(internacional, HttpStatusCode.BadRequest))["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Empresa_recusa_outra_aceita_e_conduz_com_os_documentos_exigidos()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        HttpClient equipe = funeraria.Equipe();
        await DefinirTabelaAsync(equipe);
        FuneralCriado funeral = await FuneralAsync(funeraria);
        JsonObject translado = await Cenarios.LerAsync(await RegistrarAsync(equipe, funeral, new { endereco = "Rua C, 3", cidade = "Londrina", uf = "PR" }, 120m), HttpStatusCode.Created);
        Guid id = translado["id_translado"]!.GetValue<Guid>();

        // Só entram as empresas que atendem a origem (Maringá/PR).
        Frota foraDaRegiao = await FrotaAsync(funeraria, "Frota Sul", new { ufs = new[] { "SC" } });
        Frota recusa = await FrotaAsync(funeraria, "Frota Paraná", new { ufs = new[] { "PR" } });
        Frota aceita = await FrotaAsync(funeraria, "Frota Maringá", new { cidades = new[] { new { nome = "Maringa", uf = "PR" } } });
        JsonArray disponiveis = (await equipe.GetFromJsonAsync<JsonArray>($"/api/v1/translados/{id}/empresas-disponiveis", Json, Ct))!;
        Assert.Equal(["Frota Maringá", "Frota Paraná"], disponiveis.Select(e => e!["nome"]!.GetValue<string>()).Order());
        Assert.Equal(HttpStatusCode.BadRequest, (await AtribuirAsync(equipe, id, foraDaRegiao.IdTerceiro)).StatusCode);

        // Recusa pelo portal devolve o translado para nova atribuição.
        await Cenarios.LerAsync(await AtribuirAsync(equipe, id, recusa.IdTerceiro), HttpStatusCode.OK);
        Guid atribuicao = await AtribuicaoAsync(recusa.Http, id);
        await recusa.Http.PostAsJsonAsync($"/api/v1/terceiros/atribuicoes/{atribuicao}/recusa", new { motivo = "Sem veículo." }, Json, Ct);
        await AguardarAsync(
            async () => (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/translados/{id}", Json, Ct))!["status_aceite"]?.GetValue<string>() == "RECUSADO",
            "translado devolvido");
        Assert.Empty((await recusa.Http.GetFromJsonAsync<JsonArray>("/api/v1/terceiros/translados", Json, Ct))!);

        await Cenarios.LerAsync(await AtribuirAsync(equipe, id, aceita.IdTerceiro), HttpStatusCode.OK);

        // A empresa vê só o necessário; a outra não vê nem mexe.
        JsonObject visao = (await aceita.Http.GetFromJsonAsync<JsonObject>($"/api/v1/terceiros/translados/{id}", Json, Ct))!;
        Assert.Equal("Ana Silva", visao["contato_nome"]!.GetValue<string>());
        foreach (string proibido in new[] { "valor", "cpf", "id_contrato", "id_cliente" })
        {
            Assert.DoesNotContain(proibido, visao.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await recusa.Http.GetAsync($"/api/v1/terceiros/translados/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await MotoristaAsync(aceita.Http, id)).StatusCode);

        await aceita.Http.PostAsync($"/api/v1/terceiros/atribuicoes/{await AtribuicaoAsync(aceita.Http, id)}/aceite", null, Ct);
        await AguardarAsync(
            async () => (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/translados/{id}", Json, Ct))!["status_aceite"]?.GetValue<string>() == "ACEITO",
            "aceite registrado");
        JsonObject comMotorista = await Cenarios.LerAsync(await MotoristaAsync(aceita.Http, id), HttpStatusCode.OK);
        Assert.Equal("ABC1D23", comMotorista["motorista"]!["placa"]!.GetValue<string>());

        // Sem os documentos do tipo intermunicipal, não inicia; pular etapa, também não.
        HttpResponseMessage semDocumentos = await StatusAsync(aceita.Http, $"/api/v1/terceiros/translados/{id}/status", "EM_COLETA");
        Assert.Contains("DECLARACAO_OBITO", (await Cenarios.LerAsync(semDocumentos, HttpStatusCode.Conflict))["detail"]!.GetValue<string>(), StringComparison.Ordinal);
        foreach (string tipo in new[] { "DECLARACAO_OBITO", "DOCUMENTO_FALECIDO" })
        {
            await Cenarios.LerAsync(await AnexarAsync(equipe, funeral.IdBeneficiario, tipo), HttpStatusCode.Created);
        }

        JsonObject pendencias = (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/translados/{id}/documentos", Json, Ct))!;
        Assert.True(pendencias["completo"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.BadRequest, (await StatusAsync(equipe, $"/api/v1/translados/{id}/status", "ENTREGUE")).StatusCode);

        // A empresa conduz; a família acompanha o histórico.
        await Cenarios.LerAsync(await StatusAsync(aceita.Http, $"/api/v1/terceiros/translados/{id}/status", "EM_COLETA"), HttpStatusCode.OK);
        await Cenarios.LerAsync(await StatusAsync(aceita.Http, $"/api/v1/terceiros/translados/{id}/status", "EM_TRANSITO", "Saindo de Maringá."), HttpStatusCode.OK);
        await Cenarios.LerAsync(await StatusAsync(equipe, $"/api/v1/translados/{id}/status", "ENTREGUE"), HttpStatusCode.OK);
        JsonArray historico = (await funeral.Cliente.Http.GetFromJsonAsync<JsonArray>($"/api/v1/translados/{id}/historico", Json, Ct))!;
        Assert.Equal(["AGENDADO", "EM_COLETA", "EM_TRANSITO", "ENTREGUE"], historico.Select(h => h!["para"]!.GetValue<string>()));
        Assert.Equal("Saindo de Maringá.", historico[2]!["observacao"]!.GetValue<string>());
        ClienteDeTeste outro = await funeraria.CriarClienteAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Http.GetAsync($"/api/v1/translados/{id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Http.GetAsync($"/api/v1/translados/{id}/historico", Ct)).StatusCode);
        await AguardarAsync(
            async () => (await aceita.Http.GetFromJsonAsync<JsonArray>("/api/v1/terceiros/atribuicoes?status=CONCLUIDA", Json, Ct))!.Count == 1,
            "atribuição concluída");
    }

    [Fact]
    public async Task Hub_so_deixa_entrar_quem_pode_e_entrega_o_status_na_hora()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        HttpClient equipe = funeraria.Equipe();
        await DefinirTabelaAsync(equipe);
        FuneralCriado funeral = await FuneralAsync(funeraria);
        Guid id = (await Cenarios.LerAsync(await RegistrarAsync(equipe, funeral, new { endereco = "Rua C, 3", cidade = "Londrina", uf = "PR" }, 120m), HttpStatusCode.Created))["id_translado"]!.GetValue<Guid>();
        Frota frota = await FrotaAsync(funeraria, "Frota Hub", null);
        await Cenarios.LerAsync(await AtribuirAsync(equipe, id, frota.IdTerceiro), HttpStatusCode.OK);

        var recebidas = Channel.CreateUnbounded<JsonElement>();
        await using HubConnection titular = await ConectarAsync(funeral.TokenTitular, recebidas);
        await titular.InvokeAsync("EntrarTranslado", id, Ct);
        await using HubConnection daFrota = await ConectarAsync(frota.Token, Channel.CreateUnbounded<JsonElement>());
        await daFrota.InvokeAsync("EntrarTranslado", id, Ct);

        // Outro cliente da mesma funerária não entra no grupo.
        string emailOutro = (await funeraria.CriarClienteAsync()).Email;
        await using HubConnection estranho = await ConectarAsync(await factory.EntrarAsync(funeraria.Slug, emailOutro), Channel.CreateUnbounded<JsonElement>());
        await Assert.ThrowsAsync<HubException>(() => estranho.InvokeAsync("EntrarTranslado", id, Ct));

        await Cenarios.LerAsync(await StatusAsync(equipe, $"/api/v1/translados/{id}/status", "CANCELADO", "Família desistiu."), HttpStatusCode.OK);

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        limite.CancelAfter(TimeSpan.FromSeconds(20));
        JsonElement evento = await recebidas.Reader.ReadAsync(limite.Token);
        Assert.Equal(id, evento.GetProperty("id_translado").GetGuid());
        Assert.Equal("CANCELADO", evento.GetProperty("status").GetString());
        Assert.Equal("Família desistiu.", evento.GetProperty("observacao").GetString());
    }

    [Fact]
    public async Task Funeral_cancelado_cancela_o_translado_em_aberto()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        HttpClient equipe = funeraria.Equipe();
        await DefinirTabelaAsync(equipe);
        FuneralCriado funeral = await FuneralAsync(funeraria);
        Guid id = (await Cenarios.LerAsync(await RegistrarAsync(equipe, funeral, EmMaringa, 5m), HttpStatusCode.Created))["id_translado"]!.GetValue<Guid>();

        await equipe.PatchAsJsonAsync($"/api/v1/funerais/{funeral.IdFuneral}/status", new { status = "CANCELADO" }, Json, Ct);

        await AguardarAsync(
            async () => (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/translados/{id}", Json, Ct))!["status"]!.GetValue<string>() == "CANCELADO",
            "translado cancelado");
        Assert.Equal(HttpStatusCode.Conflict, (await RegistrarAsync(equipe, funeral, EmMaringa, 5m)).StatusCode);
    }

    [Theory]
    [InlineData("""{"routes":[{"summary":{"distance":123456.0}}]}""", 123.46)]
    [InlineData("""{"erro":"inesperado"}""", null)]
    public async Task OpenRouteService_calcula_e_qualquer_falha_vira_km_manual(string rota, double? esperado)
    {
        using var http = new HttpClient(new RespostasFixas(rota));
        var gateway = new OpenRouteServiceGateway(
            http, Options.Create(new OpenRouteServiceOptions { ApiKey = "chave-de-teste" }), NullLogger<OpenRouteServiceGateway>.Instance);

        decimal? km = await gateway.CalcularKmAsync(new Endereco("Rua A", "Maringá", "PR", "BR"), new Endereco("Rua B", "Londrina", "PR", "BR"), Ct);

        Assert.Equal(esperado is null ? null : (decimal)esperado.Value, km);
    }

    [Fact]
    public async Task OpenRouteService_sem_chave_nao_chama_a_api()
    {
        using var http = new HttpClient(new RespostasFixas("{}", falhar: true));
        var gateway = new OpenRouteServiceGateway(http, Options.Create(new OpenRouteServiceOptions()), NullLogger<OpenRouteServiceGateway>.Instance);

        Assert.Null(await gateway.CalcularKmAsync(new Endereco("Rua A", "Maringá", "PR", "BR"), new Endereco("Rua B", "Londrina", "PR", "BR"), Ct));
    }

    private static readonly object[] TabelaValida =
    [
        new { de_km = 0, ate_km = 50, valor = 300 },
        new { de_km = 51, ate_km = 200, valor = 900 },
        new { de_km = 201, valor = 1800, valor_km_excedente = 3 }
    ];

    private static async Task DefinirTabelaAsync(HttpClient gestor) =>
        await Cenarios.LerAsync(await gestor.PutAsJsonAsync("/api/v1/gestao/translados/tabela-precos", new { faixas = TabelaValida }, Json, Ct), HttpStatusCode.OK);

    private sealed record FuneralCriado(Guid IdFuneral, Guid IdBeneficiario, ClienteDeTeste Cliente, string TokenTitular);

    private sealed record Frota(Guid IdTerceiro, HttpClient Http, string Token);

    private async Task<FuneralCriado> FuneralAsync(FunerariaDeTeste funeraria, Guid? plano = null)
    {
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        Guid idContrato = await cliente.ContratarAsync(plano ?? await funeraria.CriarPlanoAsync(carenciaDias: 0));
        await factory.AtivarContratoAsync(idContrato);
        HttpClient equipe = funeraria.Equipe();
        JsonObject contrato = (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/contratos/{idContrato}", Json, Ct))!;
        Guid titular = contrato["pessoas_cobertas"]![0]!["id_pessoa_coberta"]!.GetValue<Guid>();
        Guid idBeneficiario = (await Cenarios.LerAsync(await equipe.PostAsJsonAsync("/api/v1/beneficiarios", new
        {
            id_pessoa_coberta = titular,
            data_falecimento = DateOnly.FromDateTime(DateTime.Now),
            local_falecimento = "Hospital Central",
            responsavel = new { nome = "Ana Silva", telefone = "(44) 98888-7777", parentesco = "Filha" }
        }, Json, Ct), HttpStatusCode.Created))["id_beneficiario"]!.GetValue<Guid>();
        Guid idFuneral = (await Cenarios.LerAsync(await equipe.PostAsJsonAsync("/api/v1/funerais",
            new { id_beneficiario = idBeneficiario, data_hora_prevista = DateTime.UtcNow.AddDays(1), destinacao = "SEPULTAMENTO" }, Json, Ct), HttpStatusCode.Created))["id_funeral"]!.GetValue<Guid>();
        return new FuneralCriado(idFuneral, idBeneficiario, cliente, await factory.EntrarAsync(funeraria.Slug, cliente.Email));
    }

    private async Task<Frota> FrotaAsync(FunerariaDeTeste funeraria, string nome, object? area)
    {
        (_, string email) = await factory.CriarUsuarioAsync();
        JsonObject criado = await Cenarios.LerAsync(await funeraria.Equipe().PostAsJsonAsync("/api/v1/gestao/terceiros", new
        {
            tipo = "FROTA", email, nome_razao_social = nome, cpf_cnpj = DocumentosDeTeste.NovoCnpj(), area_atuacao = area
        }, Json, Ct), HttpStatusCode.Created);

        for (int i = 0; i < 75; i++)
        {
            HttpResponseMessage login = await factory.Http(funeraria.Slug).PostAsJsonAsync("/api/v1/auth/login", new { email, senha = FunerariaApiFactory.SenhaPadrao }, Json, Ct);
            if (login.IsSuccessStatusCode)
            {
                string token = (await login.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["access_token"]!.GetValue<string>();
                return new Frota(criado["id_terceiro"]!.GetValue<Guid>(), factory.Http(funeraria.Slug, token), token);
            }

            await Task.Delay(200, Ct);
        }

        throw new TimeoutException($"A empresa {nome} não conseguiu entrar.");
    }

    private static Task<HttpResponseMessage> RegistrarAsync(HttpClient http, FuneralCriado funeral, object destino, decimal? km = null) =>
        http.PostAsJsonAsync($"/api/v1/funerais/{funeral.IdFuneral}/translados",
            new { origem = EmMaringa, destino, data_prevista = DateTime.UtcNow.AddHours(8), distancia_km = km }, Json, Ct);

    private static Task<HttpResponseMessage> AtribuirAsync(HttpClient http, Guid id, Guid idTerceiro) =>
        http.PatchAsJsonAsync($"/api/v1/translados/{id}/terceiro", new { id_terceiro = idTerceiro }, Json, Ct);

    private static Task<HttpResponseMessage> MotoristaAsync(HttpClient http, Guid id) =>
        http.PutAsJsonAsync($"/api/v1/terceiros/translados/{id}/motorista",
            new { nome = "Carlos", telefone = "(44) 97777-6666", placa = "abc1d23", modelo = "Fiat Ducato" }, Json, Ct);

    private static Task<HttpResponseMessage> StatusAsync(HttpClient http, string rota, string status, string? observacao = null) =>
        http.PatchAsJsonAsync(rota, new { status, observacao }, Json, Ct);

    private static async Task<Guid> AtribuicaoAsync(HttpClient http, Guid idTranslado) =>
        (await http.GetFromJsonAsync<JsonArray>("/api/v1/terceiros/atribuicoes", Json, Ct))!
            .Single(a => a!["id_origem"]!.GetValue<Guid>() == idTranslado)!["id_atribuicao"]!.GetValue<Guid>();

    private static async Task<HttpResponseMessage> AnexarAsync(HttpClient http, Guid idBeneficiario, string tipo)
    {
        using var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(Pdf);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(arquivo, "arquivo", $"{tipo}.pdf");
        form.Add(new StringContent(tipo), "tipo");
        return await http.PostAsync($"/api/v1/beneficiarios/{idBeneficiario}/documentos", form, Ct);
    }

    private async Task<HubConnection> ConectarAsync(string token, Channel<JsonElement> recebidas)
    {
        HubConnection hub = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, TransladosHub.Caminho), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        hub.On<JsonElement>(TransladosHub.Evento, e => recebidas.Writer.TryWrite(e));
        await hub.StartAsync(Ct);
        return hub;
    }

    private static async Task AguardarAsync(Func<Task<bool>> condicao, string oQue)
    {
        for (int i = 0; i < 100; i++)
        {
            if (await condicao())
            {
                return;
            }

            await Task.Delay(200, Ct);
        }

        throw new TimeoutException($"Esperando: {oQue}.");
    }

    /// <summary>Responde a geocodificação com um ponto e a rota com o JSON do teste, sem rede.</summary>
    private sealed class RespostasFixas(string rota, bool falhar = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.False(falhar, "A API não devia ser chamada.");
            string corpo = request.Method == HttpMethod.Get
                ? """{"features":[{"geometry":{"coordinates":[-51.9,-23.4]}}]}"""
                : rota;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") });
        }
    }
}
