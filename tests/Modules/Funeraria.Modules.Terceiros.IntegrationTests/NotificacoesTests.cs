using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Terceiros.Contracts;
using Funeraria.Testing;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Terceiros.IntegrationTests;

/// <summary>NOT-02: central de notificações e entrega em tempo real pelo SignalR, a partir das atribuições (TER-02).</summary>
[Collection(ApiCollection.Nome)]
public sealed class NotificacoesTests(TerceirosApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Terceiro_recebe_o_servico_na_hora_pelo_hub_e_na_central()
    {
        Cenario c = await MontarAsync();
        var recebidas = Channel.CreateUnbounded<JsonElement>();
        await using HubConnection hub = await ConectarAsync(c.TokenTerceiro, recebidas);

        Guid idAtribuicao = await AtribuirAsync(c);

        JsonElement aoVivo = await LerAsync(recebidas);
        Assert.Equal("ATRIBUICAO_CRIADA", aoVivo.GetProperty("tipo").GetString());
        Assert.Equal($"/terceiros/atribuicoes/{idAtribuicao}", aoVivo.GetProperty("link").GetString());

        JsonObject central = (await factory.Http(c.Slug, c.TokenTerceiro).GetFromJsonAsync<JsonObject>("/api/v1/notificacoes", Json, Ct))!;
        Assert.Equal(1, central["nao_lidas"]!.GetValue<int>());
        Assert.Equal("Novo serviço: translado", central["itens"]![0]!["titulo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Recusa_avisa_os_gestores_da_funeraria()
    {
        Cenario c = await MontarAsync();
        Guid idAtribuicao = await AtribuirAsync(c);

        HttpResponseMessage recusa = await factory.Http(c.Slug, c.TokenTerceiro)
            .PostAsJsonAsync($"/api/v1/terceiros/atribuicoes/{idAtribuicao}/recusa", new { motivo = "Veículo em manutenção." }, Json, Ct);
        Assert.Equal(HttpStatusCode.OK, recusa.StatusCode);

        JsonNode aviso = await AguardarAsync(c.Slug, c.TokenGestor, "ATRIBUICAO_RECUSADA");
        Assert.Contains("Veículo em manutenção.", aviso["mensagem"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Marca_como_lida_uma_ou_todas_e_nao_mexe_na_de_outro_usuario()
    {
        Cenario c = await MontarAsync();
        await AtribuirAsync(c);
        await AtribuirAsync(c);
        HttpClient terceiro = factory.Http(c.Slug, c.TokenTerceiro);
        JsonNode primeira = await AguardarAsync(c.Slug, c.TokenTerceiro, "ATRIBUICAO_CRIADA", quantidade: 2);
        Guid id = primeira["id_notificacao"]!.GetValue<Guid>();

        Assert.Equal(HttpStatusCode.NotFound, (await factory.Http(c.Slug, c.TokenGestor).PatchAsync($"/api/v1/notificacoes/{id}/lida", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await terceiro.PatchAsync($"/api/v1/notificacoes/{id}/lida", null, Ct)).StatusCode);
        JsonObject depoisDeUma = (await terceiro.GetFromJsonAsync<JsonObject>("/api/v1/notificacoes?nao_lidas=true", Json, Ct))!;
        Assert.Equal(1, depoisDeUma["nao_lidas"]!.GetValue<int>());

        Assert.Equal(HttpStatusCode.NoContent, (await terceiro.PatchAsync("/api/v1/notificacoes/lidas", null, Ct)).StatusCode);
        JsonObject depoisDeTodas = (await terceiro.GetFromJsonAsync<JsonObject>("/api/v1/notificacoes", Json, Ct))!;
        Assert.Equal(0, depoisDeTodas["nao_lidas"]!.GetValue<int>());
        Assert.Equal(2, depoisDeTodas["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Hub_exige_login()
    {
        HttpResponseMessage response = await factory.CreateClient().PostAsync("/hubs/notificacoes/negotiate?negotiateVersion=1", null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record Cenario(Guid Tenant, string Slug, Guid IdTerceiro, string TokenTerceiro, string TokenGestor);

    /// <summary>Funerária com gestor real e um terceiro (FROTA) com conta ativa, ambos já logados.</summary>
    private async Task<Cenario> MontarAsync()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string emailGestor) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funeraria));
        (_, string emailTerceiro) = await factory.CriarUsuarioAsync();
        string tokenGestor = await factory.EntrarAsync(slug, emailGestor);

        HttpResponseMessage criado = await factory.Http(slug, TokenDeTeste.Gerar(Perfis.Funeraria, tenant)).PostAsJsonAsync("/api/v1/gestao/terceiros", new
        {
            tipo = "FROTA",
            email = emailTerceiro,
            nome_razao_social = "Translados Rápidos Ltda",
            cpf_cnpj = DocumentosDeTeste.NovoCnpj()
        }, Json, Ct);
        Guid idTerceiro = (await criado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_terceiro"]!.GetValue<Guid>();

        // O vínculo é criado pelo outbox: espera a conta conseguir entrar como terceiro.
        string? tokenTerceiro = null;
        for (int i = 0; i < 75 && tokenTerceiro is null; i++)
        {
            await Task.Delay(200, Ct);
            HttpResponseMessage login = await factory.Http(slug).PostAsJsonAsync("/api/v1/auth/login",
                new { email = emailTerceiro, senha = FunerariaApiFactory.SenhaPadrao }, Json, Ct);
            if (login.IsSuccessStatusCode)
            {
                tokenTerceiro = (await login.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["access_token"]!.GetValue<string>();
            }
        }

        return new Cenario(tenant, slug, idTerceiro, tokenTerceiro ?? throw new TimeoutException("Terceiro não conseguiu entrar."), tokenGestor);
    }

    private async Task<Guid> AtribuirAsync(Cenario c)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(c.Tenant);
        return (await scope.ServiceProvider.GetRequiredService<ITerceirosAtribuicoes>().AtribuirAsync(new NovaAtribuicao(
            c.IdTerceiro, Guid.NewGuid(), new ResumoTranslado(
                "José Lima", "Hospital Central", "Cemitério da Paz", DateTime.UtcNow.AddHours(5),
                new ContatoResponsavel("Ana Lima", "(11) 98888-7777"), [])), Ct)).Value;
    }

    private async Task<HubConnection> ConectarAsync(string token, Channel<JsonElement> recebidas)
    {
        HubConnection hub = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/notificacoes"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        hub.On<JsonElement>("notificacao", n => recebidas.Writer.TryWrite(n));
        await hub.StartAsync(Ct);
        return hub;
    }

    private static async Task<JsonElement> LerAsync(Channel<JsonElement> recebidas)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        limite.CancelAfter(TimeSpan.FromSeconds(15));
        return await recebidas.Reader.ReadAsync(limite.Token);
    }

    /// <summary>A notificação nasce pelo outbox: espera ela aparecer na central.</summary>
    private async Task<JsonNode> AguardarAsync(string slug, string token, string tipo, int quantidade = 1)
    {
        HttpClient http = factory.Http(slug, token);
        for (int i = 0; i < 75; i++)
        {
            JsonObject central = (await http.GetFromJsonAsync<JsonObject>("/api/v1/notificacoes", Json, Ct))!;
            JsonNode?[] doTipo = [.. central["itens"]!.AsArray().Where(n => n!["tipo"]!.GetValue<string>() == tipo)];
            if (doTipo.Length >= quantidade)
            {
                return doTipo[0]!;
            }

            await Task.Delay(200, Ct);
        }

        throw new TimeoutException($"Nenhuma notificação {tipo}.");
    }
}
