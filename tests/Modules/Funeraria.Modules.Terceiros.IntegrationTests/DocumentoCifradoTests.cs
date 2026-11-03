using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Funeraria.Modules.Terceiros.IntegrationTests;

/// <summary>SEG-01: CPF/CNPJ do terceiro cifrado em repouso, com unicidade pelo hash.</summary>
[Collection(ApiCollection.Nome)]
public sealed class DocumentoCifradoTests(TerceirosApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cpf_do_terceiro_pessoa_fisica_fica_cifrado_e_a_funeraria_continua_vendo_o_numero()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        string cpf = DocumentosDeTeste.NovoCpf();

        HttpResponseMessage criado = await Gestor(tenant, slug).PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(cpf), Json, Ct);

        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        JsonObject terceiro = (await criado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal(cpf, terceiro["cpf_cnpj"]!.GetValue<string>());

        (string gravado, string hash) = await LerAsync(terceiro["id_terceiro"]!.GetValue<Guid>());
        Assert.StartsWith("dp1:", gravado, StringComparison.Ordinal);
        Assert.DoesNotContain(cpf, gravado, StringComparison.Ordinal);
        Assert.Equal(factory.Services.GetRequiredService<ICriptografiaDados>().Hash(cpf), hash);
    }

    [Fact]
    public async Task Mesmo_documento_continua_recusado_na_mesma_funeraria_e_aceito_em_outra()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (Guid outroTenant, string outroSlug) = await factory.CriarFunerariaAsync();
        string cnpj = DocumentosDeTeste.NovoCnpj();
        Assert.Equal(HttpStatusCode.Created, (await Gestor(tenant, slug).PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(cnpj), Json, Ct)).StatusCode);

        HttpResponseMessage repetido = await Gestor(tenant, slug).PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(cnpj), Json, Ct);
        HttpResponseMessage outraFuneraria = await Gestor(outroTenant, outroSlug).PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(cnpj), Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal("CPF ou CNPJ já cadastrado nesta funerária.",
            (await repetido.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["detail"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Created, outraFuneraria.StatusCode);
    }

    [Fact]
    public async Task Documento_gravado_antes_da_cifra_e_cifrado_na_subida_e_continua_unico()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        string cnpj = DocumentosDeTeste.NovoCnpj();
        HttpResponseMessage criado = await Gestor(tenant, slug).PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(cnpj), Json, Ct);
        Guid id = (await criado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_terceiro"]!.GetValue<Guid>();
        await ExecutarAsync("UPDATE terceiros.terceiros SET cpf_cnpj = @cnpj, cpf_cnpj_hash = NULL WHERE id_terceiro = @id", new { cnpj, id });

        IHostedService migracao = factory.Services.GetServices<IHostedService>().Single(s => s.GetType().Name == "CifrarDocumentosLegados");
        await migracao.StartAsync(Ct);

        (string gravado, string hash) = await LerAsync(id);
        Assert.StartsWith("dp1:", gravado, StringComparison.Ordinal);
        Assert.Equal(factory.Services.GetRequiredService<ICriptografiaDados>().Hash(cnpj), hash);
        HttpResponseMessage repetido = await Gestor(tenant, slug).PostAsJsonAsync("/api/v1/gestao/terceiros", NovoTerceiro(cnpj), Json, Ct);
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
    }

    private async Task<(string Documento, string Hash)> LerAsync(Guid id)
    {
        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        return await conexao.QuerySingleAsync<(string, string)>(new CommandDefinition(
            "SELECT cpf_cnpj, cpf_cnpj_hash FROM terceiros.terceiros WHERE id_terceiro = @id", new { id }, cancellationToken: Ct));
    }

    private async Task ExecutarAsync(string sql, object parametros)
    {
        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        await conexao.ExecuteAsync(new CommandDefinition(sql, parametros, cancellationToken: Ct));
    }

    private HttpClient Gestor(Guid tenant, string slug)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(Perfis.Funeraria, tenant));
        return client;
    }

    private static object NovoTerceiro(string cpfCnpj) => new
    {
        tipo = "FROTA",
        email = $"parceiro-{Guid.NewGuid():N}@teste.localhost",
        nome_razao_social = "Parceiro Teste",
        cpf_cnpj = cpfCnpj,
        telefone = "(11) 3333-4444",
        area_atuacao = new { ufs = new[] { "sp" }, cidades = Array.Empty<object>() }
    };
}
