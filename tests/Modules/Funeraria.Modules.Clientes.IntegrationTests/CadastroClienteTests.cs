using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using static Funeraria.Modules.Clientes.IntegrationTests.ApiCliente;

namespace Funeraria.Modules.Clientes.IntegrationTests;

/// <summary>Card Criar Cliente (com os ajustes do AJU-01) e CLI-01.</summary>
[Collection(ApiCollection.Nome)]
public sealed class CadastroClienteTests(ClientesApiFactory factory)
{
    [Fact]
    public async Task Cadastro_cria_cliente_e_conta_que_ja_entra_como_cliente()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();
        Dictionary<string, object?> corpo = NovoCliente();

        HttpResponseMessage response = await factory.Cliente(slug).PostAsJsonAsync("/api/v1/clientes", corpo, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/api/v1/clientes/me", response.Headers.Location!.ToString());
        JsonObject cliente = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Null(cliente["senha"]);
        Assert.Equal("João da Silva", cliente["nome"]!.GetValue<string>());
        Assert.Equal("1985-08-20", cliente["data_nascimento"]!.GetValue<string>());
        Assert.Equal("87000000", cliente["cep"]!.GetValue<string>());
        Guid idCliente = cliente["id_cliente"]!.GetValue<Guid>();

        string token = await factory.LoginAsync(slug, (string)corpo["email"]!);
        var jwt = new JsonWebToken(token);
        Assert.Equal("CLIENTE", jwt.GetClaim("role").Value);
        Assert.Equal(idCliente.ToString(), jwt.GetClaim("id_cliente").Value);

        JsonObject me = (await factory.Cliente(slug, token).GetFromJsonAsync<JsonObject>("/api/v1/clientes/me", Json, Ct))!;
        Assert.Equal(idCliente, me["id_cliente"]!.GetValue<Guid>());
        Assert.Equal("Maringá", me["cidade"]!.GetValue<string>());
    }

    [Fact]
    public async Task Dados_invalidos_retornam_as_mensagens_do_card()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();
        var corpo = new { nome = "", cpf = "123", data_nascimento = "20/08/1985", email = "x@", senha = "", telefone = "99", cep = "1" };

        JsonObject problem = await (await factory.Cliente(slug).PostAsJsonAsync("/api/v1/clientes", corpo, Json, Ct))
            .ProblemAsync(HttpStatusCode.BadRequest);

        JsonObject erros = problem["errors"]!.AsObject();
        Assert.Equal("Nome não informado.", Primeiro(erros, "nome"));
        Assert.Equal("CPF não informado ou em formato inválido.", Primeiro(erros, "cpf"));
        Assert.Equal("Data de nascimento inválida.", Primeiro(erros, "data_nascimento"));
        Assert.Equal("E-mail em formato inválido.", Primeiro(erros, "email"));
        Assert.Equal("Senha não informada ou fora dos critérios definidos.", Primeiro(erros, "senha"));
        Assert.Equal("Telefone em formato inválido.", Primeiro(erros, "telefone"));
        Assert.Equal("CEP em formato inválido.", Primeiro(erros, "cep"));
    }

    [Fact]
    public async Task Senha_fraca_traz_a_mensagem_do_card_e_o_que_falta()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();

        JsonObject problem = await (await factory.Cliente(slug).PostAsJsonAsync("/api/v1/clientes", NovoCliente(senha: "fraca"), Json, Ct))
            .ProblemAsync(HttpStatusCode.BadRequest);

        string[] senha = problem["errors"]!["senha"]!.AsArray().Select(e => e!.GetValue<string>()).ToArray();
        Assert.Equal("Senha não informada ou fora dos critérios definidos.", senha[0]);
        Assert.Contains("A senha deve ter pelo menos 8 caracteres.", senha);
    }

    [Fact]
    public async Task Cpf_e_unico_na_funeraria_mas_pode_repetir_em_outra()
    {
        (_, string slugA) = await factory.CriarFunerariaAsync();
        (_, string slugB) = await factory.CriarFunerariaAsync();
        string cpf = DocumentosDeTeste.NovoCpf();
        await factory.CadastrarAsync(slugA, NovoCliente(cpf: cpf));

        JsonObject problem = await (await factory.Cliente(slugA).PostAsJsonAsync("/api/v1/clientes", NovoCliente(cpf: cpf), Json, Ct))
            .ProblemAsync(HttpStatusCode.Conflict);

        Assert.Equal("CPF já cadastrado.", problem["detail"]!.GetValue<string>());
        await factory.CadastrarAsync(slugB, NovoCliente(cpf: cpf));
    }

    [Fact]
    public async Task Conta_existente_de_outra_funeraria_e_reaproveitada_pela_senha()
    {
        (_, string slugA) = await factory.CriarFunerariaAsync();
        (_, string slugB) = await factory.CriarFunerariaAsync();
        string email = $"multi-{Guid.NewGuid():N}@teste.localhost";
        await factory.CadastrarAsync(slugA, NovoCliente(email: email));

        JsonObject senhaErrada = await (await factory.Cliente(slugB)
                .PostAsJsonAsync("/api/v1/clientes", NovoCliente(email: email, senha: "OutraSenha@1"), Json, Ct))
            .ProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("Auth.ContaExistente", senhaErrada["code"]!.GetValue<string>());

        await factory.CadastrarAsync(slugB, NovoCliente(email: email));

        // Mesma conta e mesma senha nas duas funerárias, cada uma com o próprio cadastro de cliente.
        var tokenA = new JsonWebToken(await factory.LoginAsync(slugA, email));
        var tokenB = new JsonWebToken(await factory.LoginAsync(slugB, email));
        Assert.Equal(tokenA.Subject, tokenB.Subject);
        Assert.NotEqual(tokenA.GetClaim("id_cliente").Value, tokenB.GetClaim("id_cliente").Value);
    }

    [Fact]
    public async Task Email_que_ja_e_cliente_da_funeraria_retorna_409()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();
        string email = $"dup-{Guid.NewGuid():N}@teste.localhost";
        await factory.CadastrarAsync(slug, NovoCliente(email: email));

        JsonObject problem = await (await factory.Cliente(slug).PostAsJsonAsync("/api/v1/clientes", NovoCliente(email: email), Json, Ct))
            .ProblemAsync(HttpStatusCode.Conflict);

        Assert.Equal("E-mail já cadastrado.", problem["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Cadastro_exige_o_endereco_de_uma_funeraria()
    {
        JsonObject problem = await (await factory.Cliente().PostAsJsonAsync("/api/v1/clientes", NovoCliente(), Json, Ct))
            .ProblemAsync(HttpStatusCode.BadRequest);

        Assert.Equal("Tenant.NaoIdentificado", problem["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Cliente_atualiza_os_proprios_dados_sem_mudar_o_cpf()
    {
        (_, _, HttpClient cliente) = await factory.ClienteLogadoAsync();
        string cpfOriginal = (await cliente.GetFromJsonAsync<JsonObject>("/api/v1/clientes/me", Json, Ct))!["cpf"]!.GetValue<string>();

        HttpResponseMessage response = await cliente.PutAsJsonAsync("/api/v1/clientes/me", new
        {
            nome = "João Silva Atualizado",
            cpf = DocumentosDeTeste.NovoCpf(),
            telefone = "(11) 3333-4444",
            cidade = "Londrina",
            estado = "PR"
        }, Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonObject me = (await cliente.GetFromJsonAsync<JsonObject>("/api/v1/clientes/me", Json, Ct))!;
        Assert.Equal("João Silva Atualizado", me["nome"]!.GetValue<string>());
        Assert.Equal("Londrina", me["cidade"]!.GetValue<string>());
        Assert.Equal(cpfOriginal, me["cpf"]!.GetValue<string>());
    }

    [Fact]
    public async Task Meus_dados_e_exclusivo_do_perfil_cliente()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();

        JsonObject anonimo = await (await factory.Cliente(slug).GetAsync("/api/v1/clientes/me", Ct)).ProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("Usuário não autenticado. Faça login para continuar.", anonimo["detail"]!.GetValue<string>());

        HttpClient gestor = factory.Cliente(slug, TokenDeTeste.Gerar("FUNERARIA", tenant));
        JsonObject proibido = await (await gestor.GetAsync("/api/v1/clientes/me", Ct)).ProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal("Auth.AcessoNegado", proibido["code"]!.GetValue<string>());
    }

    private static string Primeiro(JsonObject erros, string campo) => erros[campo]![0]!.GetValue<string>();
}
