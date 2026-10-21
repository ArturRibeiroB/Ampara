using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.Testing;
using static Funeraria.Modules.Clientes.IntegrationTests.ApiCliente;

namespace Funeraria.Modules.Clientes.IntegrationTests;

/// <summary>CLI-03: busca de clientes pela equipe da funerária.</summary>
[Collection(ApiCollection.Nome)]
public sealed class GestaoClientesTests(ClientesApiFactory factory)
{
    [Fact]
    public async Task Equipe_busca_por_nome_e_cpf_e_ve_o_detalhe_com_dependentes()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        string sobrenome = $"Busca{Guid.NewGuid():N}"[..14];
        Dictionary<string, object?> corpo = NovoCliente();
        corpo["nome"] = $"Ana {sobrenome}";
        JsonObject cliente = await factory.CadastrarAsync(slug, corpo);
        await factory.CadastrarAsync(slug, NovoCliente());
        Guid idCliente = cliente["id_cliente"]!.GetValue<Guid>();

        HttpClient titular = factory.Cliente(slug, await factory.LoginAsync(slug, (string)corpo["email"]!));
        await titular.PostAsJsonAsync("/api/v1/dependentes", new
        {
            nome = "Pedro", cpf = DocumentosDeTeste.NovoCpf(), data_nascimento = "2010-05-05", parentesco = "Filho"
        }, Json, Ct);

        HttpClient funcionario = factory.Cliente(slug, TokenDeTeste.Gerar("FUNCIONARIO", tenant));

        JsonObject porNome = (await funcionario.GetFromJsonAsync<JsonObject>(
            $"/api/v1/gestao/clientes?nome={sobrenome.ToLowerInvariant()}", Json, Ct))!;
        Assert.Equal(1, porNome["total"]!.GetValue<long>());
        JsonNode item = Assert.Single(porNome["itens"]!.AsArray())!;
        Assert.Equal(idCliente, item["id_cliente"]!.GetValue<Guid>());
        Assert.Equal(1, item["quantidade_dependentes"]!.GetValue<int>());

        string cpfFormatado = (string)corpo["cpf"]!;
        cpfFormatado = $"{cpfFormatado[..3]}.{cpfFormatado[3..6]}.{cpfFormatado[6..9]}-{cpfFormatado[9..]}";
        JsonObject porCpf = (await funcionario.GetFromJsonAsync<JsonObject>($"/api/v1/gestao/clientes?cpf={cpfFormatado}", Json, Ct))!;
        Assert.Equal(1, porCpf["total"]!.GetValue<long>());

        JsonObject todos = (await funcionario.GetFromJsonAsync<JsonObject>("/api/v1/gestao/clientes?tamanho=1", Json, Ct))!;
        Assert.Equal(2, todos["total"]!.GetValue<long>());
        Assert.Single(todos["itens"]!.AsArray());

        JsonObject detalhe = (await funcionario.GetFromJsonAsync<JsonObject>($"/api/v1/gestao/clientes/{idCliente}", Json, Ct))!;
        Assert.Equal(idCliente, detalhe["cliente"]!["id_cliente"]!.GetValue<Guid>());
        Assert.Equal("Pedro", Assert.Single(detalhe["dependentes"]!.AsArray())!["nome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Equipe_nao_ve_cliente_de_outra_funeraria_e_cliente_nao_acessa_a_gestao()
    {
        (_, Guid idClienteA, HttpClient clienteA) = await factory.ClienteLogadoAsync();
        (Guid tenantB, string slugB) = await factory.CriarFunerariaAsync();

        HttpClient equipeB = factory.Cliente(slugB, TokenDeTeste.Gerar("FUNERARIA", tenantB));
        Assert.Equal(HttpStatusCode.NotFound, (await equipeB.GetAsync($"/api/v1/gestao/clientes/{idClienteA}", Ct)).StatusCode);
        Assert.Equal(0, (await equipeB.GetFromJsonAsync<JsonObject>("/api/v1/gestao/clientes", Json, Ct))!["total"]!.GetValue<long>());

        Assert.Equal(HttpStatusCode.Forbidden, (await clienteA.GetAsync("/api/v1/gestao/clientes", Ct)).StatusCode);
    }
}
