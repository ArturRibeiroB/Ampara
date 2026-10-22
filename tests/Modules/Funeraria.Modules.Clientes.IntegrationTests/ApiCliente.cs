using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.Testing;

namespace Funeraria.Modules.Clientes.IntegrationTests;

/// <summary>Atalhos de HTTP para os testes do módulo Clientes.</summary>
internal static class ApiCliente
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static HttpClient Cliente(this FunerariaApiFactory factory, string? slug = null, string? token = null)
    {
        HttpClient client = factory.CreateClient();
        if (slug is not null)
        {
            client.DefaultRequestHeaders.Add("X-Tenant", slug);
        }

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    /// <summary>Corpo do card Criar Cliente, com CPF e e-mail únicos.</summary>
    public static Dictionary<string, object?> NovoCliente(string? email = null, string? cpf = null, string senha = "Senha@123") => new()
    {
        ["nome"] = "João da Silva",
        ["cpf"] = cpf ?? DocumentosDeTeste.NovoCpf(),
        ["data_nascimento"] = "1985-08-20",
        ["telefone"] = "(44) 99999-9999",
        ["email"] = email ?? $"cliente-{Guid.NewGuid():N}@teste.localhost",
        ["senha"] = senha,
        ["endereco"] = "Rua Exemplo, 100",
        ["cep"] = "87000-000",
        ["cidade"] = "Maringá",
        ["estado"] = "PR"
    };

    public static async Task<JsonObject> CadastrarAsync(this FunerariaApiFactory factory, string slug, Dictionary<string, object?> corpo)
    {
        HttpResponseMessage response = await factory.Cliente(slug).PostAsJsonAsync("/api/v1/clientes", corpo, Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
    }

    public static async Task<string> LoginAsync(this FunerariaApiFactory factory, string slug, string email, string senha = "Senha@123")
    {
        HttpResponseMessage response = await factory.Cliente(slug).PostAsJsonAsync("/api/v1/auth/login", new { email, senha }, Json, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["access_token"]!.GetValue<string>();
    }

    /// <summary>Cadastra um cliente e devolve o slug, o id e um cliente HTTP já logado como ele.</summary>
    public static async Task<(string Slug, Guid IdCliente, HttpClient Http)> ClienteLogadoAsync(
        this FunerariaApiFactory factory,
        string? slug = null)
    {
        slug ??= (await factory.CriarFunerariaAsync()).Slug;
        Dictionary<string, object?> corpo = NovoCliente();
        JsonObject cliente = await factory.CadastrarAsync(slug, corpo);
        string token = await factory.LoginAsync(slug, (string)corpo["email"]!);
        return (slug, cliente["id_cliente"]!.GetValue<Guid>(), factory.Cliente(slug, token));
    }

    public static async Task<JsonObject> ProblemAsync(this HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
    }
}
