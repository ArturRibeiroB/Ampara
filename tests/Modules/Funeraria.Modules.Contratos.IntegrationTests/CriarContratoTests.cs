using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Modules.Contratos.Contracts.IntegrationEvents;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using static Funeraria.Modules.Contratos.IntegrationTests.ContratosApiFactory;

namespace Funeraria.Modules.Contratos.IntegrationTests;

/// <summary>Card Criar Contrato, com os ajustes do AJU-01 (periodicidade, vencimento, status e pessoas cobertas).</summary>
[Collection(ApiCollection.Nome)]
public sealed class CriarContratoTests(ContratosApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cliente_contrata_para_si_e_dois_dependentes()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 3);

        HttpResponseMessage response = await cliente.Http.PostAsJsonAsync("/api/v1/contratos", new
        {
            id_plano = plano,
            tipo_cobertura = "TITULAR_E_DEPENDENTES",
            dependentes = cliente.Dependentes.Take(2),
            periodicidade = "MENSAL"
        }, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonObject contrato = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Guid idContrato = contrato["id_contrato"]!.GetValue<Guid>();
        Assert.EndsWith($"/api/v1/contratos/{idContrato}", response.Headers.Location!.ToString());
        Assert.Equal(cliente.IdCliente, contrato["id_cliente"]!.GetValue<Guid>());
        Assert.Equal(plano, contrato["id_plano"]!.GetValue<Guid>());
        Assert.Equal("A_PAGAR", contrato["status"]!.GetValue<string>());
        Assert.Equal("MENSAL", contrato["periodicidade"]!.GetValue<string>());
        Assert.Equal(10, contrato["dia_vencimento"]!.GetValue<int>());
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), DateOnly.Parse(contrato["data_contratacao"]!.GetValue<string>()));
        Assert.Null(contrato["data_fim"]);
        Assert.Equal(89.90m + (2 * 19.90m), contrato["valor_total"]!.GetValue<decimal>());

        JsonArray pessoas = contrato["pessoas_cobertas"]!.AsArray();
        Assert.Equal(3, pessoas.Count);
        Assert.Single(pessoas, p => p!["tipo"]!.GetValue<string>() == "TITULAR");
        Assert.Equal(
            cliente.Dependentes.Take(2).Order(),
            pessoas.Where(p => p!["tipo"]!.GetValue<string>() == "DEPENDENTE").Select(p => p!["id_dependente"]!.GetValue<Guid>()).Order());

        ContratoCriadoIntegrationEvent evento = await factory.Eventos.AguardarAsync<ContratoCriadoIntegrationEvent>(e => e.IdContrato == idContrato);
        Assert.Equal(funeraria.Tenant, evento.IdTenant);
        Assert.Equal(2, evento.QuantidadeDependentes);
        Assert.Equal("MENSAL", evento.Periodicidade);
    }

    [Fact]
    public async Task Contrato_anual_usa_os_valores_anuais_do_plano()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);

        JsonObject contrato = await CriarAsync(cliente.Http, new
        {
            id_plano = plano,
            tipo_cobertura = "TITULAR_E_DEPENDENTES",
            dependentes = cliente.Dependentes,
            periodicidade = "ANUAL"
        });

        Assert.Equal(970.92m + 214.92m, contrato["valor_total"]!.GetValue<decimal>());
        Assert.Equal(970.92m, contrato["valor_plano"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Somente_o_titular()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);

        JsonObject contrato = await CriarAsync(cliente.Http, new { id_plano = plano, tipo_cobertura = "TITULAR", periodicidade = "MENSAL" });

        Assert.Equal(89.90m, contrato["valor_total"]!.GetValue<decimal>());
        JsonNode pessoa = Assert.Single(contrato["pessoas_cobertas"]!.AsArray())!;
        Assert.Equal("TITULAR", pessoa["tipo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Somente_dependentes_so_quando_o_plano_permite()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid naoPermite = await funeraria.CriarPlanoAsync();
        Guid permite = await funeraria.CriarPlanoAsync(permiteSomenteDependente: true);
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);
        object Corpo(Guid plano) => new { id_plano = plano, tipo_cobertura = "SOMENTE_DEPENDENTES", dependentes = cliente.Dependentes, periodicidade = "MENSAL" };

        JsonObject recusado = await ProblemaAsync(await cliente.Http.PostAsJsonAsync("/api/v1/contratos", Corpo(naoPermite), Json, Ct), HttpStatusCode.BadRequest);
        Assert.Equal("Este plano não permite contratação somente para dependentes.", recusado["detail"]!.GetValue<string>());

        JsonObject contrato = await CriarAsync(cliente.Http, Corpo(permite));
        JsonNode pessoa = Assert.Single(contrato["pessoas_cobertas"]!.AsArray())!;
        Assert.Equal("DEPENDENTE", pessoa["tipo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Campos_obrigatorios_retornam_as_mensagens_do_card()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();

        JsonObject problem = await ProblemaAsync(
            await funeraria.Equipe().PostAsJsonAsync("/api/v1/gestao/contratos", new { }, Json, Ct), HttpStatusCode.BadRequest);

        JsonObject erros = problem["errors"]!.AsObject();
        Assert.Equal("Cliente titular é obrigatório.", erros["id_cliente"]![0]!.GetValue<string>());
        Assert.Equal("Plano é obrigatório.", erros["id_plano"]![0]!.GetValue<string>());
        Assert.Equal("Tipo de cobertura é obrigatório.", erros["tipo_cobertura"]![0]!.GetValue<string>());
        Assert.Equal("Periodicidade é obrigatória.", erros["periodicidade"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Dependente_numa_cobertura_so_do_titular_e_invalido()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);

        JsonObject problem = await ProblemaAsync(await cliente.Http.PostAsJsonAsync("/api/v1/contratos", new
        {
            id_plano = plano,
            tipo_cobertura = "TITULAR",
            dependentes = cliente.Dependentes,
            periodicidade = "MENSAL"
        }, Json, Ct), HttpStatusCode.BadRequest);

        Assert.Equal("Dependente selecionado é inválido.", problem["errors"]!["dependentes"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Dependente_de_outro_cliente_e_recusado()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        ClienteDeTeste outro = await funeraria.CriarClienteAsync(dependentes: 1);

        JsonObject problem = await ProblemaAsync(
            await cliente.Http.PostAsJsonAsync("/api/v1/contratos", ComDependentes(plano, outro.Dependentes), Json, Ct),
            HttpStatusCode.BadRequest);

        Assert.Equal("Dependente não pertence ao cliente informado.", problem["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Dependente_de_outra_funeraria_nao_e_encontrado()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        FunerariaDeTeste outraFuneraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        ClienteDeTeste deOutra = await outraFuneraria.CriarClienteAsync(dependentes: 1);

        JsonObject problem = await ProblemaAsync(
            await cliente.Http.PostAsJsonAsync("/api/v1/contratos", ComDependentes(plano, deOutra.Dependentes), Json, Ct),
            HttpStatusCode.NotFound);

        Assert.Equal("Dependente não encontrado.", problem["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Plano_inexistente_ou_inativo_nao_e_encontrado()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid inativo = await funeraria.CriarPlanoAsync(ativo: false);
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();

        foreach (Guid plano in new[] { inativo, Guid.NewGuid() })
        {
            JsonObject problem = await ProblemaAsync(await cliente.Http.PostAsJsonAsync("/api/v1/contratos",
                new { id_plano = plano, tipo_cobertura = "TITULAR", periodicidade = "MENSAL" }, Json, Ct), HttpStatusCode.NotFound);
            Assert.Equal("Plano não encontrado.", problem["detail"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task Quantidade_de_dependentes_acima_do_limite_do_plano_e_recusada()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync(limiteDependentes: 1);
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 2);

        JsonObject problem = await ProblemaAsync(
            await cliente.Http.PostAsJsonAsync("/api/v1/contratos", ComDependentes(plano, cliente.Dependentes), Json, Ct),
            HttpStatusCode.BadRequest);

        Assert.Equal("Quantidade de dependentes excede o limite do plano.", problem["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Segundo_contrato_no_mesmo_plano_e_recusado()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        var corpo = new { id_plano = plano, tipo_cobertura = "TITULAR", periodicidade = "MENSAL" };
        await CriarAsync(cliente.Http, corpo);

        JsonObject problem = await ProblemaAsync(await cliente.Http.PostAsJsonAsync("/api/v1/contratos", corpo, Json, Ct), HttpStatusCode.Conflict);

        Assert.Equal("Cliente já possui um contrato ativo para este plano.", problem["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Contratacoes_simultaneas_no_mesmo_plano_criam_um_contrato_so()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        var corpo = new { id_plano = plano, tipo_cobertura = "TITULAR", periodicidade = "MENSAL" };

        HttpResponseMessage[] respostas = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => cliente.Http.PostAsJsonAsync("/api/v1/contratos", corpo, Json, Ct)));

        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(respostas.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Dependente_ja_coberto_por_outro_contrato_e_recusado_e_nao_pode_ser_removido()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid primeiro = await funeraria.CriarPlanoAsync();
        Guid segundo = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);
        await CriarAsync(cliente.Http, ComDependentes(primeiro, cliente.Dependentes));

        JsonObject problem = await ProblemaAsync(
            await cliente.Http.PostAsJsonAsync("/api/v1/contratos", ComDependentes(segundo, cliente.Dependentes), Json, Ct),
            HttpStatusCode.Conflict);
        Assert.Equal("Dependente já está vinculado a um contrato ativo.", problem["detail"]!.GetValue<string>());

        // CLI-02: dependente coberto por contrato não sai do cadastro.
        HttpResponseMessage remocao = await cliente.Http.DeleteAsync($"/api/v1/dependentes/{cliente.Dependentes[0]}", Ct);
        Assert.Equal(HttpStatusCode.Conflict, remocao.StatusCode);
    }

    [Fact]
    public async Task Equipe_contrata_para_um_cliente_e_o_cliente_nao_usa_a_rota_da_equipe()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);
        var corpo = new
        {
            id_cliente = cliente.IdCliente,
            id_plano = plano,
            tipo_cobertura = "TITULAR_E_DEPENDENTES",
            dependentes = cliente.Dependentes,
            periodicidade = "MENSAL"
        };

        HttpResponseMessage pelaEquipe = await funeraria.Equipe(Perfis.Funcionario).PostAsJsonAsync("/api/v1/gestao/contratos", corpo, Json, Ct);
        HttpResponseMessage peloCliente = await cliente.Http.PostAsJsonAsync("/api/v1/gestao/contratos", corpo, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, pelaEquipe.StatusCode);
        Assert.Equal(cliente.IdCliente, (await pelaEquipe.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_cliente"]!.GetValue<Guid>());
        Assert.Equal(HttpStatusCode.Forbidden, peloCliente.StatusCode);
    }

    [Fact]
    public async Task Titular_inexistente_ou_de_outra_funeraria_nao_e_encontrado()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        FunerariaDeTeste outra = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste deOutra = await outra.CriarClienteAsync();

        foreach (Guid idCliente in new[] { Guid.NewGuid(), deOutra.IdCliente })
        {
            JsonObject problem = await ProblemaAsync(await funeraria.Equipe().PostAsJsonAsync("/api/v1/gestao/contratos",
                new { id_cliente = idCliente, id_plano = plano, tipo_cobertura = "TITULAR", periodicidade = "MENSAL" }, Json, Ct),
                HttpStatusCode.NotFound);
            Assert.Equal("Cliente titular não encontrado.", problem["detail"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task Funeraria_sem_multa_juros_e_desconto_ainda_nao_contrata()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        await factory.AlterarCobrancaAsync(funeraria.Tenant, c => c with { MultaPercentual = null });

        JsonObject problem = await ProblemaAsync(await cliente.Http.PostAsJsonAsync("/api/v1/contratos",
            new { id_plano = plano, tipo_cobertura = "TITULAR", periodicidade = "MENSAL" }, Json, Ct), HttpStatusCode.Conflict);

        Assert.Equal("Contrato.ContratacaoIndisponivel", problem["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Cliente_escolhe_o_vencimento_entre_os_dias_permitidos()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync(ModoVencimento.EscolhaCliente);
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();

        JsonObject recusado = await ProblemaAsync(await cliente.Http.PostAsJsonAsync("/api/v1/contratos",
            new { id_plano = plano, tipo_cobertura = "TITULAR", periodicidade = "MENSAL", dia_vencimento = 7 }, Json, Ct), HttpStatusCode.BadRequest);
        Assert.Equal("Dia de vencimento deve ser um destes: 5, 15.", recusado["detail"]!.GetValue<string>());

        JsonObject contrato = await CriarAsync(cliente.Http, new { id_plano = plano, tipo_cobertura = "TITULAR", periodicidade = "MENSAL", dia_vencimento = 15 });
        Assert.Equal(15, contrato["dia_vencimento"]!.GetValue<int>());
    }

    private static object ComDependentes(Guid plano, IEnumerable<Guid> dependentes) =>
        new { id_plano = plano, tipo_cobertura = "TITULAR_E_DEPENDENTES", dependentes, periodicidade = "MENSAL" };

    private static async Task<JsonObject> CriarAsync(HttpClient http, object corpo)
    {
        HttpResponseMessage response = await http.PostAsJsonAsync("/api/v1/contratos", corpo, Json, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
    }

    private static async Task<JsonObject> ProblemaAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.True(response.StatusCode == status, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
    }
}
