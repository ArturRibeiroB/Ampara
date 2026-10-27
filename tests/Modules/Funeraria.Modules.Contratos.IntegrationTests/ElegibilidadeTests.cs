using Funeraria.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Contratos.Contracts;
using Microsoft.Extensions.DependencyInjection;
using static Funeraria.Modules.Contratos.IntegrationTests.ContratosApiFactory;

namespace Funeraria.Modules.Contratos.IntegrationTests;

/// <summary>CON-04: GET /contratos/{id}/elegibilidade e a consulta usada pelo registro do beneficiário.</summary>
[Collection(ApiCollection.Nome)]
public sealed class ElegibilidadeTests(ContratosApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Contrato_a_pagar_nao_e_elegivel_e_depois_do_pagamento_vale_a_carencia()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid comCarencia = await funeraria.CriarPlanoAsync(carenciaDias: 90);
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync(dependentes: 1);
        Guid idContrato = await ContratarAsync(cliente, comCarencia);

        JsonArray antes = (await cliente.Http.GetFromJsonAsync<JsonArray>($"/api/v1/contratos/{idContrato}/elegibilidade", Json, Ct))!;
        Assert.Equal(2, antes.Count);
        Assert.All(antes, p => Assert.Equal("Contrato aguardando o pagamento da primeira cobrança.", p!["motivo"]!.GetValue<string>()));

        await factory.AtivarContratoAsync(idContrato);

        JsonArray depois = (await funeraria.Equipe().GetFromJsonAsync<JsonArray>($"/api/v1/contratos/{idContrato}/elegibilidade", Json, Ct))!;
        JsonNode titular = depois[0]!;
        Assert.Equal("TITULAR", titular["tipo"]!.GetValue<string>());
        Assert.Equal("João da Silva", titular["nome"]!.GetValue<string>());
        Assert.False(titular["elegivel"]!.GetValue<bool>());
        DateOnly fim = DateOnly.FromDateTime(DateTime.Now).AddDays(90);
        Assert.Equal(fim, DateOnly.Parse(titular["carencia_cumprida_em"]!.GetValue<string>()));
        Assert.Equal($"Em carência até {fim:dd/MM/yyyy}.", titular["motivo"]!.GetValue<string>());
        Assert.Equal("Dependente 1", depois[1]!["nome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Plano_sem_carencia_e_contrato_ativo_ja_e_elegivel_e_a_api_do_beneficiario_concorda()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid semCarencia = await funeraria.CriarPlanoAsync(carenciaDias: 0);
        ClienteDeTeste cliente = await funeraria.CriarClienteAsync();
        Guid idContrato = await ContratarAsync(cliente, semCarencia);
        await factory.AtivarContratoAsync(idContrato);

        JsonNode titular = Assert.Single((await cliente.Http.GetFromJsonAsync<JsonArray>($"/api/v1/contratos/{idContrato}/elegibilidade", Json, Ct))!)!;
        Assert.True(titular["elegivel"]!.GetValue<bool>());
        Assert.Null(titular["motivo"]);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(funeraria.Tenant);
        ElegibilidadeDto? dto = await scope.ServiceProvider.GetRequiredService<IContratosApi>()
            .VerificarElegibilidadeAsync(titular["id_pessoa_coberta"]!.GetValue<Guid>(), Ct);
        Assert.True(dto!.Elegivel);
        Assert.Equal(idContrato, dto.IdContrato);
        Assert.Equal(cliente.IdCliente, dto.IdCliente);
    }

    [Fact]
    public async Task Cliente_nao_consulta_contrato_de_outro()
    {
        FunerariaDeTeste funeraria = await factory.CriarFunerariaContratavelAsync();
        Guid plano = await funeraria.CriarPlanoAsync();
        ClienteDeTeste dono = await funeraria.CriarClienteAsync();
        ClienteDeTeste outro = await funeraria.CriarClienteAsync();
        Guid idContrato = await ContratarAsync(dono, plano);

        HttpResponseMessage response = await outro.Http.GetAsync($"/api/v1/contratos/{idContrato}/elegibilidade", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> ContratarAsync(ClienteDeTeste cliente, Guid plano)
    {
        HttpResponseMessage response = await cliente.Http.PostAsJsonAsync("/api/v1/contratos", new
        {
            id_plano = plano,
            tipo_cobertura = cliente.Dependentes.Count == 0 ? "TITULAR" : "TITULAR_E_DEPENDENTES",
            dependentes = cliente.Dependentes,
            periodicidade = "MENSAL"
        }, Json, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_contrato"]!.GetValue<Guid>();
    }
}
