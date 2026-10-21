using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Testing;

namespace Funeraria.Modules.Tenants.IntegrationTests;

/// <summary>Gestão das funerárias pelo ADMIN (TEN-02) e configurações da funerária (TEN-03).</summary>
[Collection(ApiCollection.Nome)]
public sealed class AdminEConfiguracoesTests(TenantsApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_cadastra_lista_e_ativa_funeraria()
    {
        string slug = $"adm-{Guid.NewGuid():N}"[..20];
        HttpClient admin = Cliente(TokenDeTeste.Gerar(Perfis.Admin, null));

        HttpResponseMessage criada = await admin.PostAsJsonAsync("/api/v1/admin/tenants", new
        {
            nome_fantasia = "Funerária do Admin",
            razao_social = "Funerária do Admin Ltda",
            cnpj = DocumentosDeTeste.NovoCnpj(),
            slug,
            email_contato = $"contato@{slug}.localhost",
            gestor = new { nome = "Gestor", email = $"gestor@{slug}.localhost" }
        }, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        Guid id = (await criada.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_tenant"]!.GetValue<Guid>();
        Assert.EndsWith($"/api/v1/admin/tenants/{id}", criada.Headers.Location!.ToString());

        JsonObject pagina = (await admin.GetFromJsonAsync<JsonObject>($"/api/v1/admin/tenants?busca={slug}&tamanho=5", Json, Ct))!;
        Assert.Equal(1, pagina["total"]!.GetValue<long>());
        Assert.Equal(5, pagina["tamanho"]!.GetValue<int>());
        JsonNode item = Assert.Single(pagina["itens"]!.AsArray())!;
        Assert.Equal("ADMIN", item["origem_cadastro"]!.GetValue<string>());
        Assert.Equal("PENDENTE", item["status"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.NoContent, (await AlterarStatusAsync(admin, id, "ATIVA")).StatusCode);

        HttpResponseMessage voltar = await AlterarStatusAsync(admin, id, "PENDENTE");
        Assert.Equal(HttpStatusCode.Conflict, voltar.StatusCode);
        Assert.Equal("Funeraria.TransicaoInvalida", (await voltar.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Rotas_do_admin_sao_exclusivas_do_admin()
    {
        (Guid tenant, _) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Cliente(TokenDeTeste.Gerar(Perfis.Funeraria, tenant));

        Assert.Equal(HttpStatusCode.Forbidden, (await gestor.GetAsync("/api/v1/admin/tenants", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente().GetAsync("/api/v1/admin/tenants", Ct)).StatusCode);
    }

    [Fact]
    public async Task Suspensao_bloqueia_a_funeraria_na_hora()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient publico = Cliente(slug: slug);
        HttpClient gestorSemEndereco = Cliente(TokenDeTeste.Gerar(Perfis.Funeraria, tenant));

        // Primeira leitura coloca a funerária no cache de resolução de tenant.
        Assert.Equal(HttpStatusCode.OK, (await publico.GetAsync("/api/v1/tenant/configuracoes", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await gestorSemEndereco.GetAsync("/api/v1/gestao/configuracoes", Ct)).StatusCode);

        HttpClient admin = Cliente(TokenDeTeste.Gerar(Perfis.Admin, null));
        Assert.Equal(HttpStatusCode.NoContent, (await AlterarStatusAsync(admin, tenant, "SUSPENSA")).StatusCode);

        HttpResponseMessage bloqueada = await publico.GetAsync("/api/v1/tenant/configuracoes", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, bloqueada.StatusCode);
        Assert.Equal("Tenant.Suspensa", (await bloqueada.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["code"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Forbidden, (await gestorSemEndereco.GetAsync("/api/v1/gestao/configuracoes", Ct)).StatusCode);
    }

    [Fact]
    public async Task Gestor_configura_identidade_visual_e_parametros_de_cobranca()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Cliente(TokenDeTeste.Gerar(Perfis.Funeraria, tenant), slug);

        HttpResponseMessage salvar = await gestor.PutAsJsonAsync("/api/v1/gestao/configuracoes", Configuracoes(multa: 2), Json, Ct);

        Assert.Equal(HttpStatusCode.NoContent, salvar.StatusCode);

        JsonObject publico = (await Cliente(slug: slug).GetFromJsonAsync<JsonObject>("/api/v1/tenant/configuracoes", Json, Ct))!;
        Assert.Equal("Paz Eterna", publico["identidade_visual"]!["nome_exibicao"]!.GetValue<string>());
        Assert.Equal("#112233", publico["identidade_visual"]!["cor_primaria"]!.GetValue<string>());
        Assert.Equal("SP", publico["identidade_visual"]!["endereco"]!["uf"]!.GetValue<string>());
        Assert.Null(publico["parametros"]);

        JsonObject completo = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/configuracoes", Json, Ct))!;
        Assert.True(completo["cobranca_configurada"]!.GetValue<bool>());
        Assert.Equal("ESCOLHA_CLIENTE", completo["parametros"]!["cobranca"]!["modo_vencimento"]!.GetValue<string>());
        Assert.Equal([5, 15], completo["parametros"]!["cobranca"]!["dias_vencimento_permitidos"]!.AsArray().Select(d => d!.GetValue<int>()));
    }

    [Fact]
    public async Task Multa_acima_do_limite_do_cdc_e_recusada()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient gestor = Cliente(TokenDeTeste.Gerar(Perfis.Funeraria, tenant), slug);

        HttpResponseMessage response = await gestor.PutAsJsonAsync("/api/v1/gestao/configuracoes", Configuracoes(multa: 3), Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JsonObject erros = (await response.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["errors"]!.AsObject();
        Assert.Equal("Multa deve estar entre 0% e 2% (limite do CDC).", erros["parametros.cobranca.multa_percentual"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Funcionario_nao_altera_configuracoes()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        HttpClient funcionario = Cliente(TokenDeTeste.Gerar(Perfis.Funcionario, tenant), slug);

        HttpResponseMessage response = await funcionario.PutAsJsonAsync("/api/v1/gestao/configuracoes", Configuracoes(multa: 2), Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task<HttpResponseMessage> AlterarStatusAsync(HttpClient admin, Guid id, string status) =>
        admin.PatchAsJsonAsync($"/api/v1/admin/tenants/{id}/status", new { status }, Json, Ct);

    private HttpClient Cliente(string? accessToken = null, string? slug = null)
    {
        HttpClient client = factory.CreateClient();
        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (slug is not null)
        {
            client.DefaultRequestHeaders.Add("X-Tenant", slug);
        }

        return client;
    }

    private static object Configuracoes(decimal multa) => new
    {
        identidade_visual = new
        {
            nome_exibicao = "Paz Eterna",
            cor_primaria = "#112233",
            cor_secundaria = "#AABBCC",
            telefone = "(11) 3333-4444",
            endereco = new { logradouro = "Rua das Flores", numero = "10", cidade = "São Paulo", uf = "SP", cep = "01001-000" },
            horario_atendimento = "24 horas"
        },
        parametros = new
        {
            cobranca = new
            {
                modo_vencimento = "ESCOLHA_CLIENTE",
                dia_vencimento_fixo = 10,
                dias_vencimento_permitidos = new[] { 5, 15 },
                tolerancia_dias = 3,
                multa_percentual = multa,
                juros_mensal_percentual = 1,
                desconto_anual_percentual = 10,
                reajuste_anual_percentual = 5
            },
            lembretes = new { dias_antes = new[] { 3, 1 }, dias_depois = new[] { 1 } }
        }
    };
}
