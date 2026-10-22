using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.S3.Model;
using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Tenants.IntegrationTests;

/// <summary>Logo da funerária (TEN-03) gravado no armazenamento S3 (INF-18).</summary>
[Collection(ApiCollection.Nome)]
public sealed class LogoTests(TenantsApiFactory factory)
{
    /// <summary>PNG de 1x1 pixel.</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Gestor_envia_o_logo_que_fica_publico_pela_api_e_privado_no_armazenamento()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Gestor(tenant, slug);

        HttpResponseMessage enviado = await EnviarAsync(gestor, Png, "logo.png");

        Assert.Equal(HttpStatusCode.OK, enviado.StatusCode);
        string logoUrl = (await enviado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["logo_url"]!.GetValue<string>();
        Assert.StartsWith($"http://api.teste.localhost/api/v1/funerarias/{slug}/logo?v=", logoUrl, StringComparison.Ordinal);

        JsonObject publico = (await Anonimo(slug).GetFromJsonAsync<JsonObject>("/api/v1/tenant/configuracoes", Json, Ct))!;
        Assert.Equal(logoUrl, publico["identidade_visual"]!["logo_url"]!.GetValue<string>());

        // Sem token e sem X-Tenant, como num cliente de e-mail.
        HttpResponseMessage logo = await Anonimo().GetAsync(new Uri(logoUrl).PathAndQuery, Ct);
        Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        Assert.Equal("image/png", logo.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await logo.Content.ReadAsByteArrayAsync(Ct));
        Assert.True(logo.Headers.CacheControl!.Public);

        string chave = Assert.Single(await ChavesAsync(tenant));
        Assert.StartsWith($"tenants/{tenant}/logo/", chave, StringComparison.Ordinal);
        Assert.EndsWith(".png", chave, StringComparison.Ordinal);

        // O bucket é privado: sem assinatura, nada feito.
        using var http = new HttpClient();
        HttpResponseMessage direto = await http.GetAsync($"{factory.Armazenamento!.ServiceUrl}/{ArmazenamentoDeTeste.Bucket}/{chave}", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, direto.StatusCode);
    }

    [Fact]
    public async Task Novo_logo_substitui_o_anterior_no_armazenamento()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Gestor(tenant, slug);

        await EnviarAsync(gestor, Png, "logo.png");
        string primeira = Assert.Single(await ChavesAsync(tenant));
        byte[] outroPng = [.. Png, 0];
        await EnviarAsync(gestor, outroPng, "logo-novo.png");

        string atual = Assert.Single(await ChavesAsync(tenant));
        Assert.NotEqual(primeira, atual);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "logo.png")]
    [InlineData("%PDF-1.7 documento", "logo.pdf")]
    [InlineData("texto qualquer", "logo.jpg")]
    public async Task Arquivo_que_nao_e_imagem_aceita_e_recusado_pelo_conteudo(string conteudo, string nome)
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();

        HttpResponseMessage response = await EnviarAsync(Gestor(tenant, slug), Encoding.UTF8.GetBytes(conteudo), nome);

        Assert.Equal("Tipo de arquivo não permitido. Envie PNG, JPEG, WEBP.", await ErroDoArquivoAsync(response));
        Assert.Empty(await ChavesAsync(tenant));
    }

    [Fact]
    public async Task Logo_acima_de_2_mb_e_recusado()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        byte[] grande = [.. Png, .. new byte[(2 * 1024 * 1024) + 1]];

        HttpResponseMessage response = await EnviarAsync(Gestor(tenant, slug), grande, "logo.png");

        Assert.Equal("Arquivo maior que o permitido (máximo de 2 MB).", await ErroDoArquivoAsync(response));
    }

    [Fact]
    public async Task Envio_sem_arquivo_e_recusado()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        using var form = new MultipartFormDataContent { { new StringContent("x"), "outro_campo" } };

        HttpResponseMessage response = await Gestor(tenant, slug).PostAsync("/api/v1/gestao/configuracoes/logo", form, Ct);

        Assert.Equal("Envie o arquivo do logo.", await ErroDoArquivoAsync(response));
    }

    [Fact]
    public async Task Logo_nao_muda_pelo_put_das_configuracoes()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Gestor(tenant, slug);
        HttpResponseMessage enviado = await EnviarAsync(gestor, Png, "logo.png");
        string logoUrl = (await enviado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["logo_url"]!.GetValue<string>();

        JsonObject configuracoes = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/configuracoes", Json, Ct))!;
        JsonObject visual = configuracoes["identidade_visual"]!.AsObject();
        visual["logo_url"] = "https://rastreador.exemplo/pixel.png";
        HttpResponseMessage salvar = await gestor.PutAsJsonAsync(
            "/api/v1/gestao/configuracoes", new { identidade_visual = visual, parametros = configuracoes["parametros"] }, Json, Ct);

        Assert.Equal(HttpStatusCode.NoContent, salvar.StatusCode);
        JsonObject depois = (await Anonimo(slug).GetFromJsonAsync<JsonObject>("/api/v1/tenant/configuracoes", Json, Ct))!;
        Assert.Equal(logoUrl, depois["identidade_visual"]!["logo_url"]!.GetValue<string>());
    }

    [Fact]
    public async Task Gestor_remove_o_logo()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Gestor(tenant, slug);
        await EnviarAsync(gestor, Png, "logo.png");

        HttpResponseMessage removido = await gestor.DeleteAsync("/api/v1/gestao/configuracoes/logo", Ct);

        Assert.Equal(HttpStatusCode.NoContent, removido.StatusCode);
        Assert.Empty(await ChavesAsync(tenant));
        Assert.Equal(HttpStatusCode.NotFound, (await Anonimo().GetAsync($"/api/v1/funerarias/{slug}/logo", Ct)).StatusCode);
        JsonObject publico = (await Anonimo(slug).GetFromJsonAsync<JsonObject>("/api/v1/tenant/configuracoes", Json, Ct))!;
        Assert.Null(publico["identidade_visual"]!["logo_url"]);
    }

    [Fact]
    public async Task Funcionario_nao_envia_logo()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient funcionario = Anonimo(slug);
        funcionario.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(Perfis.Funcionario, tenant));

        HttpResponseMessage response = await EnviarAsync(funcionario, Png, "logo.png");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Armazenamento_nao_entrega_arquivo_de_outra_funeraria_e_gera_link_temporario()
    {
        (Guid tenantA, string slugA) = await factory.CriarFunerariaAsync();
        (Guid tenantB, _) = await factory.CriarFunerariaAsync();
        await EnviarAsync(Gestor(tenantA, slugA), Png, "logo.png");
        string chave = Assert.Single(await ChavesAsync(tenantA));

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantB);
        var armazenamento = scope.ServiceProvider.GetRequiredService<IArmazenamentoArquivos>();

        Assert.Null(await armazenamento.AbrirAsync(chave, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => armazenamento.GerarLinkTemporarioAsync(chave, TimeSpan.FromMinutes(5), Ct));
        await armazenamento.RemoverAsync(chave, Ct);
        Assert.Single(await ChavesAsync(tenantA));

        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantA);
        Uri link = await armazenamento.GerarLinkTemporarioAsync(chave, TimeSpan.FromMinutes(5), Ct);
        using var http = new HttpClient();
        Assert.Equal(Png, await http.GetByteArrayAsync(link, Ct));
    }

    private async Task<List<string>> ChavesAsync(Guid tenant)
    {
        using var s3 = factory.Armazenamento!.CriarCliente();
        ListObjectsV2Response objetos = await s3.ListObjectsV2Async(
            new ListObjectsV2Request { BucketName = ArmazenamentoDeTeste.Bucket, Prefix = ChavesArquivo.Prefixo(tenant) }, Ct);
        return objetos.S3Objects?.Select(o => o.Key).ToList() ?? [];
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, byte[] conteudo, string nome)
    {
        using var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(conteudo);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(arquivo, "arquivo", nome);
        return await client.PostAsync("/api/v1/gestao/configuracoes/logo", form, Ct);
    }

    private static async Task<string> ErroDoArquivoAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonObject problem = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        return problem["errors"]!["arquivo"]![0]!.GetValue<string>();
    }

    private HttpClient Gestor(Guid tenant, string slug)
    {
        HttpClient client = Anonimo(slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(Perfis.Funeraria, tenant));
        return client;
    }

    private HttpClient Anonimo(string? slug = null)
    {
        HttpClient client = factory.CreateClient();
        if (slug is not null)
        {
            client.DefaultRequestHeaders.Add("X-Tenant", slug);
        }

        return client;
    }
}
