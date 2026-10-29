using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Testing;

namespace Funeraria.Modules.Funeral.IntegrationTests;

/// <summary>CER-01: documentos do falecido, com download por link temporário só para a equipe interna e o titular.</summary>
[Collection(ApiCollection.Nome)]
public sealed class DocumentosTests(FuneralApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = JsonApi.Opcoes;

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Equipe_anexa_titular_baixa_e_ninguem_mais_ve()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        FuneralDeTeste funeral = await factory.CriarFuneralAsync(funeraria);
        HttpClient funcionario = funeraria.Equipe(Perfis.Funcionario);
        string rota = $"/api/v1/beneficiarios/{funeral.IdBeneficiario}/documentos";

        JsonObject anexado = await Cenarios.LerAsync(await EnviarAsync(funcionario, rota, Pdf, "declaracao.pdf", "DECLARACAO_OBITO"), HttpStatusCode.Created);
        Assert.Equal("DECLARACAO_OBITO", anexado["tipo"]!.GetValue<string>());
        Assert.Equal("application/pdf", anexado["content_type"]!.GetValue<string>());
        Assert.Null(anexado["chave"]);

        // Tipo de arquivo fora da lista, tipo de documento faltando e cliente anexando: recusados.
        Assert.Equal(HttpStatusCode.BadRequest, (await EnviarAsync(funcionario, rota, "texto"u8.ToArray(), "nota.txt", "OUTRO")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnviarAsync(funcionario, rota, Pdf, "x.pdf", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await EnviarAsync(funeral.Cliente.Http, rota, Pdf, "x.pdf", "OUTRO")).StatusCode);

        // O titular lista e baixa pelo link temporário; outro cliente não acha; o consultor não vê documentos.
        JsonArray lista = (await funeral.Cliente.Http.GetFromJsonAsync<JsonArray>(rota, Json, Ct))!;
        Guid idDocumento = Assert.Single(lista)!["id_documento"]!.GetValue<Guid>();
        JsonObject link = (await funeral.Cliente.Http.GetFromJsonAsync<JsonObject>($"{rota}/{idDocumento}/download", Json, Ct))!;
        using (var semToken = new HttpClient())
        {
            Assert.Equal(Pdf, await semToken.GetByteArrayAsync(link["url"]!.GetValue<string>(), Ct));
        }

        ClienteDeTeste outro = await funeraria.CriarClienteAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await outro.Http.GetAsync(rota, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await funeraria.Equipe(Perfis.Consultor).GetAsync(rota, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await funcionario.DeleteAsync($"{rota}/{idDocumento}", Ct)).StatusCode);
        Assert.Empty((await funcionario.GetFromJsonAsync<JsonArray>(rota, Json, Ct))!);
    }

    internal static async Task<HttpResponseMessage> EnviarAsync(HttpClient http, string rota, byte[] conteudo, string nome, string? tipo)
    {
        using var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(arquivo, "arquivo", nome);
        if (tipo is not null)
        {
            form.Add(new StringContent(tipo), "tipo");
        }

        return await http.PostAsync(rota, form, Ct);
    }
}
