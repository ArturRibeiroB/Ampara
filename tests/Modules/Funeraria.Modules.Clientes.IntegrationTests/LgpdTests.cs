using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static Funeraria.Modules.Clientes.IntegrationTests.ApiCliente;

namespace Funeraria.Modules.Clientes.IntegrationTests;

/// <summary>SEG-01: aceite dos termos com versão e data, e exportação dos dados pelo titular.</summary>
[Collection(ApiCollection.Nome)]
public sealed class LgpdTests(ClientesApiFactory factory)
{
    [Fact]
    public async Task Aceite_no_cadastro_registra_as_versoes_vigentes()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();
        Dictionary<string, object?> corpo = NovoCliente();
        corpo["aceite_termos"] = true;
        await factory.CadastrarAsync(slug, corpo);
        HttpClient cliente = factory.Cliente(slug, await factory.LoginAsync(slug, (string)corpo["email"]!));

        JsonObject termos = (await cliente.GetFromJsonAsync<JsonObject>("/api/v1/auth/termos", Json, Ct))!;

        Assert.False(termos["pendente"]!.GetValue<bool>());
        Assert.Equal("1.0", termos["termos_de_uso"]!["versao_aceita"]!.GetValue<string>());
        Assert.NotNull(termos["politica_de_privacidade"]!["aceito_em"]);
    }

    [Fact]
    public async Task Sem_aceite_fica_pendente_e_so_aceita_a_versao_vigente()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();
        (_, _, HttpClient cliente) = await factory.ClienteLogadoAsync(slug);

        JsonObject antes = (await cliente.GetFromJsonAsync<JsonObject>("/api/v1/auth/termos", Json, Ct))!;
        HttpResponseMessage antiga = await cliente.PostAsJsonAsync("/api/v1/auth/termos/aceite",
            new { versao_termos_de_uso = "0.9", versao_politica_de_privacidade = "1.0" }, Json, Ct);
        HttpResponseMessage vigente = await cliente.PostAsJsonAsync("/api/v1/auth/termos/aceite",
            new { versao_termos_de_uso = "1.0", versao_politica_de_privacidade = "1.0" }, Json, Ct);

        Assert.True(antes["pendente"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Conflict, antiga.StatusCode);
        Assert.Equal(HttpStatusCode.OK, vigente.StatusCode);
        Assert.False((await vigente.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["pendente"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Titular_exporta_cadastro_dependentes_contratos_e_aceites()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();
        Dictionary<string, object?> corpo = NovoCliente();
        corpo["aceite_termos"] = true;
        await factory.CadastrarAsync(slug, corpo);
        HttpClient cliente = factory.Cliente(slug, await factory.LoginAsync(slug, (string)corpo["email"]!));
        await cliente.PostAsJsonAsync("/api/v1/dependentes", new
        {
            nome = "Pedro",
            cpf = Testing.DocumentosDeTeste.NovoCpf(),
            data_nascimento = "2012-03-04",
            parentesco = "Filho"
        }, Json, Ct);

        JsonObject exportacao = (await cliente.GetFromJsonAsync<JsonObject>("/api/v1/clientes/me/exportacao", Json, Ct))!;

        Assert.Equal((string)corpo["cpf"]!, exportacao["titular"]!["cpf"]!.GetValue<string>());
        Assert.Equal("Pedro", Assert.Single(exportacao["dependentes"]!.AsArray())!["nome"]!.GetValue<string>());
        Assert.Empty(exportacao["contratos"]!.AsArray());
        Assert.Equal(["POLITICA_DE_PRIVACIDADE", "TERMOS_DE_USO"],
            exportacao["aceites_termos"]!.AsArray().Select(a => a!["documento"]!.GetValue<string>()).Order());
    }
}
