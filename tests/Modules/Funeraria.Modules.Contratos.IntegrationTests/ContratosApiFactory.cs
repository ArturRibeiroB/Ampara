using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Contratos.Contracts.IntegrationEvents;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Modules.Tenants.Infrastructure.Database;
using Funeraria.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Contratos.IntegrationTests;

public sealed class ContratosApiFactory : FunerariaApiFactory, IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => IniciarAsync();

    protected override void ConfigurarServicos(IServiceCollection services) =>
        services.AddScoped<IIntegrationEventHandler<ContratoCriadoIntegrationEvent>, CapturarEvento<ContratoCriadoIntegrationEvent>>();

    /// <summary>Funerária ativa com multa, juros e desconto preenchidos, que é o que libera a contratação (TEN-03).</summary>
    public async Task<FunerariaDeTeste> CriarFunerariaContratavelAsync(ModoVencimento modo = ModoVencimento.Fixo)
    {
        (Guid tenant, string slug) = await CriarFunerariaAsync();
        await AlterarCobrancaAsync(tenant, c => c with
        {
            ModoVencimento = modo,
            DiasVencimentoPermitidos = [5, 15],
            MultaPercentual = 2,
            JurosMensalPercentual = 1,
            DescontoAnualPercentual = 10
        });
        return new FunerariaDeTeste(this, tenant, slug);
    }

    public async Task AlterarCobrancaAsync(Guid tenant, Func<ParametrosCobranca, ParametrosCobranca> alterar)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        var funeraria = await context.Set<FunerariaEntity>().SingleAsync(f => f.Id == tenant);
        funeraria.AtualizarConfiguracoes(funeraria.IdentidadeVisual, funeraria.Parametros with { Cobranca = alterar(funeraria.Parametros.Cobranca) });
        await context.SaveChangesAsync();
    }

    public HttpClient Http(string slug, string? token = null)
    {
        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", slug);
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    /// <summary>Atalhos de uma funerária dos testes: equipe, planos e clientes com dependentes.</summary>
    public sealed class FunerariaDeTeste(ContratosApiFactory factory, Guid tenant, string slug)
    {
        public Guid Tenant { get; } = tenant;

        public string Slug { get; } = slug;

        public HttpClient Equipe(string perfil = Perfis.Funeraria) => factory.Http(Slug, TokenDeTeste.Gerar(perfil, Tenant));

        public async Task<Guid> CriarPlanoAsync(int limiteDependentes = 4, bool permiteSomenteDependente = false, bool ativo = true)
        {
            HttpResponseMessage criado = await Equipe().PostAsJsonAsync("/api/v1/gestao/planos", new
            {
                nome = $"Plano {Guid.NewGuid():N}"[..20],
                descricao = "Plano de teste",
                valor_mensal = 89.90m,
                valor_anual = 970.92m,
                valor_dependente_mensal = 19.90m,
                valor_dependente_anual = 214.92m,
                carencia_dias = 90,
                limite_dependentes = limiteDependentes,
                permite_somente_dependente = permiteSomenteDependente
            }, Json, Ct);
            Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
            Guid id = (await criado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_plano"]!.GetValue<Guid>();

            if (!ativo)
            {
                HttpResponseMessage inativado = await Equipe().PatchAsJsonAsync($"/api/v1/gestao/planos/{id}/status", new { ativo = false }, Json, Ct);
                Assert.True(inativado.IsSuccessStatusCode, await inativado.Content.ReadAsStringAsync(Ct));
            }

            return id;
        }

        /// <summary>Cadastra um cliente (card Criar Cliente), entra com ele e cadastra os dependentes pedidos.</summary>
        public async Task<ClienteDeTeste> CriarClienteAsync(int dependentes = 0)
        {
            string email = $"cliente-{Guid.NewGuid():N}@teste.localhost";
            HttpResponseMessage cadastro = await factory.Http(Slug).PostAsJsonAsync("/api/v1/clientes", new
            {
                nome = "João da Silva",
                cpf = DocumentosDeTeste.NovoCpf(),
                data_nascimento = "1985-08-20",
                telefone = "(44) 99999-9999",
                email,
                senha = SenhaPadrao,
                endereco = "Rua Exemplo, 100",
                cep = "87000-000",
                cidade = "Maringá",
                estado = "PR"
            }, Json, Ct);
            Assert.Equal(HttpStatusCode.Created, cadastro.StatusCode);
            Guid idCliente = (await cadastro.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_cliente"]!.GetValue<Guid>();

            HttpResponseMessage login = await factory.Http(Slug).PostAsJsonAsync("/api/v1/auth/login", new { email, senha = SenhaPadrao }, Json, Ct);
            string token = (await login.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["access_token"]!.GetValue<string>();
            HttpClient http = factory.Http(Slug, token);

            var ids = new List<Guid>();
            for (int i = 0; i < dependentes; i++)
            {
                HttpResponseMessage dependente = await http.PostAsJsonAsync("/api/v1/dependentes", new
                {
                    nome = $"Dependente {i + 1}",
                    cpf = DocumentosDeTeste.NovoCpf(),
                    data_nascimento = "2010-01-15",
                    parentesco = "Filho(a)"
                }, Json, Ct);
                Assert.Equal(HttpStatusCode.Created, dependente.StatusCode);
                ids.Add((await dependente.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_dependente"]!.GetValue<Guid>());
            }

            return new ClienteDeTeste(idCliente, http, ids);
        }
    }
}

public sealed record ClienteDeTeste(Guid IdCliente, HttpClient Http, IReadOnlyList<Guid> Dependentes);

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<ContratosApiFactory>
{
    public const string Nome = "Api";
}
