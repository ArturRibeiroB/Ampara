using Funeraria.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using static Funeraria.Modules.Contratos.IntegrationTests.ContratosApiFactory;

namespace Funeraria.Modules.Contratos.IntegrationTests;

/// <summary>CON-01: consulta pelo cliente (só os próprios) e pela equipe (todos, com filtros).</summary>
[Collection(ApiCollection.Nome)]
public sealed class ConsultarContratosTests(ContratosApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cliente_lista_e_ve_o_detalhe_dos_proprios_contratos()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);
        Guid idContrato = await ContratarAsync(cliente.Http, plano, cliente.Dependentes);

        JsonArray lista = (await cliente.Http.GetFromJsonAsync<JsonArray>("/api/v1/contratos", Json, Ct))!;
        JsonNode resumo = Assert.Single(lista)!;
        Assert.Equal(idContrato, resumo["id_contrato"]!.GetValue<Guid>());
        Assert.Equal("João da Silva", resumo["nome_titular"]!.GetValue<string>());
        Assert.Equal(2, resumo["quantidade_pessoas"]!.GetValue<int>());

        JsonObject detalhe = (await cliente.Http.GetFromJsonAsync<JsonObject>($"/api/v1/contratos/{idContrato}", Json, Ct))!;
        Assert.Equal(cliente.IdCliente, detalhe["titular"]!["id_cliente"]!.GetValue<Guid>());
        Assert.Equal(90, detalhe["carencia_dias"]!.GetValue<int>());
        JsonArray pessoas = detalhe["pessoas_cobertas"]!.AsArray();
        Assert.Equal(["TITULAR", "DEPENDENTE"], pessoas.Select(p => p!["tipo"]!.GetValue<string>()));
        Assert.Equal("Dependente 1", pessoas[1]!["nome"]!.GetValue<string>());
        DateOnly inclusao = DateOnly.Parse(pessoas[1]!["data_inclusao"]!.GetValue<string>());
        Assert.Equal(inclusao.AddDays(90), DateOnly.Parse(pessoas[1]!["carencia_cumprida_em"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Cliente_nao_ve_contrato_de_outro_cliente()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste dono = await funeraria.CriarClienteAsync();
        ClienteDeTeste outro = await funeraria.CriarClienteAsync();
        Guid idContrato = await ContratarAsync(dono.Http, plano, []);

        HttpResponseMessage response = await outro.Http.GetAsync($"/api/v1/contratos/{idContrato}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty((await outro.Http.GetFromJsonAsync<JsonArray>("/api/v1/contratos", Json, Ct))!);
    }

    [Fact]
    public async Task Equipe_ve_o_detalhe_de_qualquer_contrato_da_funeraria_mas_nao_de_outra()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        FunerariaDeTeste outra = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        Guid idContrato = await ContratarAsync(cliente.Http, plano, []);

        HttpResponseMessage daFuneraria = await funeraria.Equipe(Perfis.Funcionario).GetAsync($"/api/v1/contratos/{idContrato}", Ct);
        HttpResponseMessage deOutra = await outra.Equipe().GetAsync($"/api/v1/contratos/{idContrato}", Ct);

        Assert.Equal(HttpStatusCode.OK, daFuneraria.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deOutra.StatusCode);
    }

    [Fact]
    public async Task Equipe_busca_por_status_plano_e_cpf_com_paginacao()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid essencial = await funeraria.CriarPlanoAsync();
        Guid familiar = await funeraria.CriarPlanoAsync();
        ClienteDeTeste ana = await funeraria.CriarClienteAsync();
        ClienteDeTeste bruno = await funeraria.CriarClienteAsync();
        await ContratarAsync(ana.Http, essencial, []);
        await ContratarAsync(ana.Http, familiar, []);
        Guid doBruno = await ContratarAsync(bruno.Http, essencial, []);
        HttpClient equipe = funeraria.Equipe();

        JsonObject todos = (await equipe.GetFromJsonAsync<JsonObject>("/api/v1/gestao/contratos?status=A_PAGAR&tamanho=2", Json, Ct))!;
        Assert.Equal(3, todos["total"]!.GetValue<int>());
        Assert.Equal(2, todos["itens"]!.AsArray().Count);

        JsonObject porPlano = (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/gestao/contratos?id_plano={familiar}", Json, Ct))!;
        Assert.Equal(familiar, Assert.Single(porPlano["itens"]!.AsArray())!["id_plano"]!.GetValue<Guid>());

        string cpfBruno = (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/contratos/{doBruno}", Json, Ct))!["titular"]!["cpf"]!.GetValue<string>();
        JsonObject porCpf = (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/gestao/contratos?cpf={cpfBruno}", Json, Ct))!;
        Assert.Equal(doBruno, Assert.Single(porCpf["itens"]!.AsArray())!["id_contrato"]!.GetValue<Guid>());

        JsonObject ativos = (await equipe.GetFromJsonAsync<JsonObject>("/api/v1/gestao/contratos?status=ATIVO", Json, Ct))!;
        Assert.Equal(0, ativos["total"]!.GetValue<int>());

        JsonObject cpfSemCliente = (await equipe.GetFromJsonAsync<JsonObject>("/api/v1/gestao/contratos?cpf=52998224725", Json, Ct))!;
        Assert.Equal(0, cpfSemCliente["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Status_invalido_na_busca_retorna_400()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();

        HttpResponseMessage response = await funeraria.Equipe().GetAsync("/api/v1/gestao/contratos?status=VENCIDO", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cliente_nao_usa_a_busca_da_equipe()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.Http.GetAsync("/api/v1/gestao/contratos", Ct)).StatusCode);
    }

    private static async Task<Guid> ContratarAsync(HttpClient http, Guid plano, IReadOnlyList<Guid> dependentes)
    {
        HttpResponseMessage response = await http.PostAsJsonAsync("/api/v1/contratos", new
        {
            id_plano = plano,
            tipo_cobertura = dependentes.Count == 0 ? "TITULAR" : "TITULAR_E_DEPENDENTES",
            dependentes,
            periodicidade = "MENSAL"
        }, Json, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_contrato"]!.GetValue<Guid>();
    }
}
