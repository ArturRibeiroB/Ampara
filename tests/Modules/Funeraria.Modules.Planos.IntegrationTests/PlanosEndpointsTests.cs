using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Modules.Planos.Contracts.IntegrationEvents;
using Funeraria.Testing;

namespace Funeraria.Modules.Planos.IntegrationTests;

[Collection(ApiCollection.Nome)]
public sealed class PlanosEndpointsTests(PlanosApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Gestor_cria_plano_que_aparece_na_listagem_publica_e_publica_PlanoCriado()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();

        HttpResponseMessage criado = await Gestor(tenant).PostAsJsonAsync("/api/v1/gestao/planos", NovoPlano("Essencial"), Json, Ct);

        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        Guid idPlano = (await criado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_plano"]!.GetValue<Guid>();
        Assert.EndsWith($"/api/v1/gestao/planos/{idPlano}", criado.Headers.Location!.ToString());

        JsonArray planos = (await Publico(slug).GetFromJsonAsync<JsonArray>("/api/v1/planos", Json, Ct))!;
        JsonNode plano = Assert.Single(planos)!;
        Assert.Equal(idPlano, plano["id_plano"]!.GetValue<Guid>());
        Assert.Equal(89.90m, plano["valor_mensal"]!.GetValue<decimal>());

        PlanoCriadoIntegrationEvent evento = await factory.Eventos.AguardarAsync<PlanoCriadoIntegrationEvent>(e => e.IdPlano == idPlano);
        Assert.Equal(tenant, evento.IdTenant);
        Assert.Equal("Essencial", evento.Nome);
    }

    [Fact]
    public async Task Tenant_pode_ser_resolvido_pelo_subdominio()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        await CriarPlanoAsync(tenant, "Familiar");

        HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/planos");
        request.Headers.Host = $"{slug}.localhost";

        HttpResponseMessage response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonArray planos = (await response.Content.ReadFromJsonAsync<JsonArray>(Json, Ct))!;
        Assert.Equal("Familiar", Assert.Single(planos)!["nome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Criar_plano_sem_token_retorna_401()
    {
        HttpResponseMessage response = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/gestao/planos", NovoPlano("Essencial"), Json, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Funcionario_nao_pode_gerenciar_planos()
    {
        (Guid tenant, _) = await factory.CriarFunerariaAsync();

        HttpResponseMessage response = await Cliente(Perfis.Funcionario, tenant)
            .PostAsJsonAsync("/api/v1/gestao/planos", NovoPlano("Essencial"), Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Plano_invalido_retorna_400_com_erros_por_campo()
    {
        (Guid tenant, _) = await factory.CriarFunerariaAsync();
        var invalido = NovoPlano("") with { ValorMensal = 0 };

        HttpResponseMessage response = await Gestor(tenant).PostAsJsonAsync("/api/v1/gestao/planos", invalido, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonObject problem = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("Validacao", problem["code"]!.GetValue<string>());
        Assert.NotNull(problem["traceId"]);
        JsonObject erros = problem["errors"]!.AsObject();
        Assert.Equal("Nome do plano é obrigatório.", erros["nome"]![0]!.GetValue<string>());
        Assert.Equal("Valor mensal deve ser maior que zero.", erros["valor_mensal"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Nome_duplicado_na_mesma_funeraria_retorna_409_mas_e_permitido_em_outra()
    {
        (Guid tenantA, _) = await factory.CriarFunerariaAsync();
        (Guid tenantB, _) = await factory.CriarFunerariaAsync();
        await CriarPlanoAsync(tenantA, "Premium");

        HttpResponseMessage duplicado = await Gestor(tenantA).PostAsJsonAsync("/api/v1/gestao/planos", NovoPlano("Premium"), Json, Ct);
        HttpResponseMessage outraFuneraria = await Gestor(tenantB).PostAsJsonAsync("/api/v1/gestao/planos", NovoPlano("Premium"), Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
        JsonObject problem = (await duplicado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("Plano.NomeDuplicado", problem["code"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Created, outraFuneraria.StatusCode);
    }

    [Fact]
    public async Task Funeraria_nao_enxerga_nem_altera_planos_de_outra()
    {
        (Guid tenantA, _) = await factory.CriarFunerariaAsync();
        (Guid tenantB, string slugB) = await factory.CriarFunerariaAsync();
        Guid planoA = await CriarPlanoAsync(tenantA, "Essencial");

        JsonArray publicosB = (await Publico(slugB).GetFromJsonAsync<JsonArray>("/api/v1/planos", Json, Ct))!;
        HttpResponseMessage detalhe = await Gestor(tenantB).GetAsync($"/api/v1/gestao/planos/{planoA}", Ct);
        HttpResponseMessage alterar = await Gestor(tenantB).PatchAsJsonAsync(
            $"/api/v1/gestao/planos/{planoA}/status", new { ativo = false }, Json, Ct);

        Assert.Empty(publicosB);
        Assert.Equal(HttpStatusCode.NotFound, detalhe.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, alterar.StatusCode);
    }

    [Fact]
    public async Task Token_de_uma_funeraria_no_endereco_de_outra_retorna_403()
    {
        (Guid tenantA, _) = await factory.CriarFunerariaAsync();
        (_, string slugB) = await factory.CriarFunerariaAsync();

        HttpClient client = Gestor(tenantA);
        client.DefaultRequestHeaders.Add("X-Tenant", slugB);

        HttpResponseMessage response = await client.GetAsync("/api/v1/gestao/planos", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        JsonObject problem = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("Tenant.AcessoNegado", problem["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Listagem_publica_sem_funeraria_retorna_400()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/v1/planos", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonObject problem = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal("Tenant.NaoIdentificado", problem["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Funeraria_inexistente_retorna_404()
    {
        HttpResponseMessage response = await Publico("nao-existe").GetAsync("/api/v1/planos", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Plano_desativado_some_da_listagem_publica_mas_continua_na_gestao()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        Guid idPlano = await CriarPlanoAsync(tenant, "Essencial");

        HttpResponseMessage desativar = await Gestor(tenant).PatchAsJsonAsync(
            $"/api/v1/gestao/planos/{idPlano}/status", new { ativo = false }, Json, Ct);

        Assert.Equal(HttpStatusCode.NoContent, desativar.StatusCode);
        Assert.Empty((await Publico(slug).GetFromJsonAsync<JsonArray>("/api/v1/planos", Json, Ct))!);
        Assert.Equal(HttpStatusCode.NotFound, (await Publico(slug).GetAsync($"/api/v1/planos/{idPlano}", Ct)).StatusCode);

        JsonArray gestao = (await Gestor(tenant).GetFromJsonAsync<JsonArray>("/api/v1/gestao/planos", Json, Ct))!;
        Assert.False(Assert.Single(gestao)!["ativo"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Atualizar_valor_grava_configuracoes_em_jsonb_e_publica_ValorPlanoAlterado()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        Guid idPlano = await CriarPlanoAsync(tenant, "Essencial");
        var atualizado = NovoPlano("Essencial") with
        {
            ValorMensal = 99.90m,
            Configuracoes = new
            {
                versao = 1,
                translado = new { km_incluso = 150, internacional = false },
                velorio = new { horas_incluidas = 24, sala_vip = true },
                cremacao_inclusa = true,
                transmissao_online = false,
                dependentes = new { idade_maxima_filhos = 21 }
            }
        };

        HttpResponseMessage response = await Gestor(tenant).PutAsJsonAsync($"/api/v1/gestao/planos/{idPlano}", atualizado, Json, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        JsonObject plano = (await Publico(slug).GetFromJsonAsync<JsonObject>($"/api/v1/planos/{idPlano}", Json, Ct))!;
        Assert.Equal(99.90m, plano["valor_mensal"]!.GetValue<decimal>());
        Assert.Equal(150, plano["configuracoes"]!["translado"]!["km_incluso"]!.GetValue<int>());
        Assert.True(plano["configuracoes"]!["velorio"]!["sala_vip"]!.GetValue<bool>());
        Assert.Equal(21, plano["configuracoes"]!["dependentes"]!["idade_maxima_filhos"]!.GetValue<int>());

        ValorPlanoAlteradoIntegrationEvent evento =
            await factory.Eventos.AguardarAsync<ValorPlanoAlteradoIntegrationEvent>(e => e.IdPlano == idPlano);
        Assert.Equal(99.90m, evento.ValorMensal);
    }

    private async Task<Guid> CriarPlanoAsync(Guid tenant, string nome)
    {
        HttpResponseMessage response = await Gestor(tenant).PostAsJsonAsync("/api/v1/gestao/planos", NovoPlano(nome), Json, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_plano"]!.GetValue<Guid>();
    }

    private HttpClient Gestor(Guid tenant) => Cliente(Perfis.Funeraria, tenant);

    private HttpClient Cliente(string perfil, Guid tenant)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(perfil, tenant));
        return client;
    }

    private HttpClient Publico(string slug)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", slug);
        return client;
    }

    private static PlanoBody NovoPlano(string nome) => new(
        Nome: nome,
        Descricao: "Plano de teste",
        ValorMensal: 89.90m,
        ValorAnual: 970.92m,
        ValorDependenteMensal: 19.90m,
        ValorDependenteAnual: 214.92m,
        CarenciaDias: 90,
        LimiteDependentes: 4,
        PermiteSomenteDependente: false,
        Configuracoes: null);

    private sealed record PlanoBody(
        string Nome,
        string? Descricao,
        decimal ValorMensal,
        decimal ValorAnual,
        decimal ValorDependenteMensal,
        decimal ValorDependenteAnual,
        int CarenciaDias,
        int LimiteDependentes,
        bool PermiteSomenteDependente,
        object? Configuracoes);
}
