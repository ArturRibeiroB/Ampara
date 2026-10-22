using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.Testing;
using static Funeraria.Modules.Clientes.IntegrationTests.ApiCliente;

namespace Funeraria.Modules.Clientes.IntegrationTests;

/// <summary>Card Criar Dependente e CLI-02.</summary>
[Collection(ApiCollection.Nome)]
public sealed class DependentesTests(ClientesApiFactory factory)
{
    [Fact]
    public async Task Titular_cadastra_dependente_associado_a_ele_mesmo_que_mande_outro_id_cliente()
    {
        (_, Guid idCliente, HttpClient titular) = await factory.ClienteLogadoAsync();
        Dictionary<string, object?> corpo = NovoDependente();
        corpo["id_cliente"] = Guid.NewGuid();

        HttpResponseMessage response = await titular.PostAsJsonAsync("/api/v1/dependentes", corpo, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonObject dependente = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal(idCliente, dependente["id_cliente"]!.GetValue<Guid>());
        Assert.Equal("Esposa", dependente["parentesco"]!.GetValue<string>());
        Assert.EndsWith($"/api/v1/dependentes/{dependente["id_dependente"]}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Sem_login_retorna_401_com_a_mensagem_do_card()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();

        JsonObject problem = await (await factory.Cliente(slug).PostAsJsonAsync("/api/v1/dependentes", NovoDependente(), Json, Ct))
            .ProblemAsync(HttpStatusCode.Unauthorized);

        Assert.StartsWith("Usuário não autenticado.", problem["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Dados_invalidos_retornam_as_mensagens_do_card()
    {
        (_, _, HttpClient titular) = await factory.ClienteLogadoAsync();

        JsonObject erros = (await (await titular.PostAsJsonAsync("/api/v1/dependentes",
                    new { nome = "", cpf = "123", data_nascimento = "2999-01-01", parentesco = "", telefone = "1", email = "x" }, Json, Ct))
                .ProblemAsync(HttpStatusCode.BadRequest))["errors"]!.AsObject();

        Assert.Equal("Nome é obrigatório.", erros["nome"]![0]!.GetValue<string>());
        Assert.Equal("CPF inválido.", erros["cpf"]![0]!.GetValue<string>());
        Assert.Equal("Data de nascimento inválida.", erros["data_nascimento"]![0]!.GetValue<string>());
        Assert.Equal("Parentesco é obrigatório.", erros["parentesco"]![0]!.GetValue<string>());
        Assert.Equal("Telefone inválido.", erros["telefone"]![0]!.GetValue<string>());
        Assert.Equal("E-mail inválido.", erros["email"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Mesmo_dependente_nao_entra_duas_vezes_para_o_mesmo_titular()
    {
        (string slug, _, HttpClient titular) = await factory.ClienteLogadoAsync();
        (_, _, HttpClient outroTitular) = await factory.ClienteLogadoAsync(slug);
        string cpf = DocumentosDeTeste.NovoCpf();
        Assert.Equal(HttpStatusCode.Created, (await titular.PostAsJsonAsync("/api/v1/dependentes", NovoDependente(cpf), Json, Ct)).StatusCode);

        JsonObject problem = await (await titular.PostAsJsonAsync("/api/v1/dependentes", NovoDependente(cpf), Json, Ct))
            .ProblemAsync(HttpStatusCode.Conflict);

        Assert.Equal("Este CPF já está cadastrado entre os dependentes.", problem["detail"]!.GetValue<string>());
        // A regra do card vale por titular: outro titular pode ter a mesma pessoa como dependente.
        Assert.Equal(HttpStatusCode.Created, (await outroTitular.PostAsJsonAsync("/api/v1/dependentes", NovoDependente(cpf), Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Titular_nao_pode_ser_dependente_de_si_mesmo()
    {
        (_, _, HttpClient titular) = await factory.ClienteLogadoAsync();
        string cpfTitular = (await titular.GetFromJsonAsync<JsonObject>("/api/v1/clientes/me", Json, Ct))!["cpf"]!.GetValue<string>();

        JsonObject problem = await (await titular.PostAsJsonAsync("/api/v1/dependentes", NovoDependente(cpfTitular), Json, Ct))
            .ProblemAsync(HttpStatusCode.Conflict);

        Assert.Equal("Dependente.CpfDoTitular", problem["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Titular_lista_edita_e_remove_seus_dependentes()
    {
        (_, _, HttpClient titular) = await factory.ClienteLogadoAsync();
        string cpf = DocumentosDeTeste.NovoCpf();
        JsonObject criado = (await (await titular.PostAsJsonAsync("/api/v1/dependentes", NovoDependente(cpf), Json, Ct))
            .Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Guid id = criado["id_dependente"]!.GetValue<Guid>();

        HttpResponseMessage editado = await titular.PutAsJsonAsync($"/api/v1/dependentes/{id}", new
        {
            nome = "Maria Souza",
            data_nascimento = "1986-02-15",
            parentesco = "Cônjuge",
            telefone = "(44) 98888-7777"
        }, Json, Ct);
        Assert.Equal(HttpStatusCode.OK, editado.StatusCode);

        JsonObject detalhe = (await titular.GetFromJsonAsync<JsonObject>($"/api/v1/dependentes/{id}", Json, Ct))!;
        Assert.Equal("Maria Souza", detalhe["nome"]!.GetValue<string>());
        Assert.Equal("1986-02-15", detalhe["data_nascimento"]!.GetValue<string>());
        Assert.Equal(cpf, detalhe["cpf"]!.GetValue<string>());
        Assert.Single((await titular.GetFromJsonAsync<JsonArray>("/api/v1/dependentes", Json, Ct))!);

        Assert.Equal(HttpStatusCode.NoContent, (await titular.DeleteAsync($"/api/v1/dependentes/{id}", Ct)).StatusCode);

        Assert.Empty((await titular.GetFromJsonAsync<JsonArray>("/api/v1/dependentes", Json, Ct))!);
        Assert.Equal(HttpStatusCode.NotFound, (await titular.GetAsync($"/api/v1/dependentes/{id}", Ct)).StatusCode);
        // Remoção lógica: a mesma pessoa pode ser cadastrada de novo.
        Assert.Equal(HttpStatusCode.Created, (await titular.PostAsJsonAsync("/api/v1/dependentes", NovoDependente(cpf), Json, Ct)).StatusCode);
    }

    [Fact]
    public async Task Dependente_de_outro_titular_responde_404()
    {
        (string slug, _, HttpClient dono) = await factory.ClienteLogadoAsync();
        (_, _, HttpClient outro) = await factory.ClienteLogadoAsync(slug);
        Guid id = (await (await dono.PostAsJsonAsync("/api/v1/dependentes", NovoDependente(), Json, Ct))
            .Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_dependente"]!.GetValue<Guid>();

        JsonObject problem = await (await outro.GetAsync($"/api/v1/dependentes/{id}", Ct)).ProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("Dependente não encontrado.", problem["detail"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NotFound, (await outro.DeleteAsync($"/api/v1/dependentes/{id}", Ct)).StatusCode);
        Assert.Empty((await outro.GetFromJsonAsync<JsonArray>("/api/v1/dependentes", Json, Ct))!);
    }

    private static Dictionary<string, object?> NovoDependente(string? cpf = null) => new()
    {
        ["nome"] = "Maria da Silva",
        ["cpf"] = cpf ?? DocumentosDeTeste.NovoCpf(),
        ["data_nascimento"] = "1985-08-20",
        ["parentesco"] = "Esposa",
        ["telefone"] = "(44) 99999-9999",
        ["email"] = "maria@email.com"
    };
}
