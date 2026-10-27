using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Funeral.IntegrationTests;

public sealed class FuneralApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public ValueTask InitializeAsync() => IniciarAsync();
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<FuneralApiFactory>
{
    public const string Nome = "Api";
}

/// <summary>FUN-02: preferências de funeral registradas em vida.</summary>
[Collection(ApiCollection.Nome)]
public sealed class PreferenciasTests(FuneralApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Titular_registra_a_propria_preferencia_e_ve_o_que_o_plano_cobre()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid urna = await funeraria.CriarItemCatalogoAsync("Urna padrão");
        Guid coroa = await funeraria.CriarItemCatalogoAsync("Coroa de flores");
        Guid plano = await funeraria.CriarPlanoAsync();
        await funeraria.Equipe().PutAsJsonAsync($"/api/v1/gestao/planos/{plano}/coberturas",
            new { coberturas = new[] { new { id_item_catalogo = coroa, quantidade = 1 } } }, Json, Ct);
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);
        await cliente.ContratarAsync(plano);

        HttpResponseMessage salvo = await cliente.Http.PutAsJsonAsync($"/api/v1/preferencias-funeral/{cliente.IdCliente}", new
        {
            destinacao = "CREMACAO",
            tipo_cerimonia = "RELIGIOSA",
            crenca_religiosa = "Católica",
            musicas = new[] { "Ave Maria" },
            mensagens = "Sem tristeza, por favor.",
            itens = new object[] { new { id_item_catalogo = urna, quantidade = 1 }, new { id_item_catalogo = coroa, quantidade = 2 } }
        }, Json, Ct);

        Assert.Equal(HttpStatusCode.OK, salvo.StatusCode);
        JsonArray pessoas = (await cliente.Http.GetFromJsonAsync<JsonArray>("/api/v1/preferencias-funeral", Json, Ct))!;
        Assert.Equal(["TITULAR", "DEPENDENTE"], pessoas.Select(p => p!["tipo"]!.GetValue<string>()));
        Assert.Null(pessoas[1]!["preferencia"]);

        JsonNode preferencia = pessoas[0]!["preferencia"]!;
        Assert.Equal("CREMACAO", preferencia["destinacao"]!.GetValue<string>());
        Assert.Equal("Católica", preferencia["crenca_religiosa"]!.GetValue<string>());
        JsonArray itens = preferencia["itens"]!.AsArray();
        JsonNode itemUrna = itens.Single(i => i!["id_item_catalogo"]!.GetValue<Guid>() == urna)!;
        JsonNode itemCoroa = itens.Single(i => i!["id_item_catalogo"]!.GetValue<Guid>() == coroa)!;
        Assert.False(itemUrna["coberto_pelo_plano"]!.GetValue<bool>());
        Assert.True(itemCoroa["coberto_pelo_plano"]!.GetValue<bool>());
        Assert.Equal(1, itemCoroa["quantidade_coberta"]!.GetValue<int>());
        Assert.Equal("Coroa de flores", itemCoroa["nome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Crenca_religiosa_fica_cifrada_no_banco()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();

        await cliente.Http.PutAsJsonAsync($"/api/v1/preferencias-funeral/{cliente.IdCliente}",
            new { tipo_cerimonia = "RELIGIOSA", crenca_religiosa = "Espírita" }, Json, Ct);

        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        string gravado = await conexao.QuerySingleAsync<string>(new CommandDefinition(
            "SELECT crenca_religiosa FROM funeral.preferencias WHERE id_pessoa = @id", new { id = cliente.IdCliente }, cancellationToken: Ct));
        Assert.StartsWith("dp1:", gravado, StringComparison.Ordinal);
        Assert.DoesNotContain("Espírita", gravado, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preferencia_do_dependente_e_atualizada_sem_duplicar()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);
        Guid dependente = cliente.Dependentes[0];

        await cliente.Http.PutAsJsonAsync($"/api/v1/preferencias-funeral/{dependente}", new { destinacao = "SEPULTAMENTO" }, Json, Ct);
        HttpResponseMessage alterada = await cliente.Http.PutAsJsonAsync($"/api/v1/preferencias-funeral/{dependente}",
            new { destinacao = "CREMACAO", observacoes = "Cinzas no mar" }, Json, Ct);

        JsonObject resposta = (await alterada.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("DEPENDENTE", resposta["tipo"]!.GetValue<string>());
        Assert.Equal("CREMACAO", resposta["preferencia"]!["destinacao"]!.GetValue<string>());
        JsonArray pessoas = (await cliente.Http.GetFromJsonAsync<JsonArray>("/api/v1/preferencias-funeral", Json, Ct))!;
        Assert.Equal("Cinzas no mar", pessoas[1]!["preferencia"]!["observacoes"]!.GetValue<string>());
    }

    [Fact]
    public async Task Pessoa_de_outro_titular_nao_e_encontrada_e_item_inativo_e_recusado()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        ClienteDeTeste outro = await funeraria.CriarClienteAsync(dependentes: 1);
        Guid inativo = await funeraria.CriarItemCatalogoAsync("Urna antiga");
        await funeraria.Equipe().PatchAsJsonAsync($"/api/v1/gestao/catalogo/{inativo}/status", new { ativo = false }, Json, Ct);

        HttpResponseMessage deOutro = await cliente.Http.PutAsJsonAsync($"/api/v1/preferencias-funeral/{outro.Dependentes[0]}", new { }, Json, Ct);
        HttpResponseMessage itemInativo = await cliente.Http.PutAsJsonAsync($"/api/v1/preferencias-funeral/{cliente.IdCliente}",
            new { itens = new[] { new { id_item_catalogo = inativo, quantidade = 1 } } }, Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, deOutro.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, itemInativo.StatusCode);
    }

    [Fact]
    public async Task So_o_titular_usa_as_preferencias()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();

        HttpResponseMessage response = await funeraria.Equipe(Perfis.Funcionario).GetAsync("/api/v1/preferencias-funeral", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
