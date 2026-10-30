using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.Modules.Contratos.Domain.Contratos;
using Funeraria.Modules.Contratos.Infrastructure.Database;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Modules.Tenants.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Testing;

/// <summary>Serialização igual à da API (snake_case).</summary>
public static class JsonApi
{
    public static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}

/// <summary>
/// Cenários usados por vários projetos de teste: funerária que já pode vender, planos, clientes com dependentes e
/// contratos ativos. Falhas na montagem lançam exceção com a resposta da API.
/// </summary>
public static class Cenarios
{
    public static HttpClient Http(this FunerariaApiFactory factory, string slug, string? token = null)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", slug);
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    /// <summary>Cria uma conta com a <see cref="FunerariaApiFactory.SenhaPadrao"/> e os vínculos informados.</summary>
    public static async Task<(Guid IdUsuario, string Email)> CriarUsuarioAsync(
        this FunerariaApiFactory factory, params (Guid IdTenant, PerfilVinculo Perfil)[] vinculos)
    {
        string email = $"{Guid.NewGuid():N}@teste.localhost";

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Nome = "Usuário de Teste",
            CriadoEm = DateTime.UtcNow
        };

        IdentityResult result = await userManager.CreateAsync(usuario, FunerariaApiFactory.SenhaPadrao);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        var context = scope.ServiceProvider.GetRequiredService<IdentidadeDbContext>();
        foreach ((Guid idTenant, PerfilVinculo perfil) in vinculos)
        {
            context.Add(UsuarioVinculo.Criar(usuario.Id, idTenant, perfil, idCliente: perfil == PerfilVinculo.Cliente ? Guid.NewGuid() : null));
        }

        await context.SaveChangesAsync();
        return (usuario.Id, email);
    }

    /// <summary>Entra pela API e devolve o access token (com o sub da conta real).</summary>
    public static async Task<string> EntrarAsync(this FunerariaApiFactory factory, string? slug, string email)
    {
        HttpClient client = factory.CreateClient();
        if (slug is not null)
        {
            client.DefaultRequestHeaders.Add("X-Tenant", slug);
        }

        HttpResponseMessage login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, senha = FunerariaApiFactory.SenhaPadrao }, JsonApi.Opcoes);
        return (await LerAsync(login, HttpStatusCode.OK))["access_token"]!.GetValue<string>();
    }

    /// <summary>Funerária ativa com os parâmetros de cobrança preenchidos, que é o que libera a contratação (TEN-03).</summary>
    public static async Task<FunerariaDeTeste> CriarFunerariaContratavelAsync(this FunerariaApiFactory factory, ModoVencimento modo = ModoVencimento.Fixo)
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        await factory.AlterarCobrancaAsync(tenant, c => c with
        {
            ModoVencimento = modo,
            DiasVencimentoPermitidos = [5, 15],
            MultaPercentual = 2,
            JurosMensalPercentual = 1,
            DescontoAnualPercentual = 10
        });
        return new FunerariaDeTeste(factory, tenant, slug);
    }

    public static async Task AlterarCobrancaAsync(this FunerariaApiFactory factory, Guid tenant, Func<ParametrosCobranca, ParametrosCobranca> alterar)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        FunerariaEntity funeraria = await context.Set<FunerariaEntity>().SingleAsync(f => f.Id == tenant);
        funeraria.AtualizarConfiguracoes(funeraria.IdentidadeVisual, funeraria.Parametros with { Cobranca = alterar(funeraria.Parametros.Cobranca) });
        await context.SaveChangesAsync();
    }

    /// <summary>Simula o pagamento da primeira cobrança (o COB-01 fará isso pelo webhook do Stripe).</summary>
    public static async Task AtivarContratoAsync(this FunerariaApiFactory factory, Guid idContrato)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ContratosDbContext>();
        Contrato contrato = await context.Set<Contrato>().IgnoreQueryFilters().SingleAsync(c => c.Id == idContrato);
        contrato.Ativar();
        await context.SaveChangesAsync();
    }

    /// <summary>O cliente contrata o plano para si e para todos os dependentes cadastrados (mensal).</summary>
    public static async Task<Guid> ContratarAsync(this ClienteDeTeste cliente, Guid plano)
    {
        HttpResponseMessage response = await cliente.Http.PostAsJsonAsync("/api/v1/contratos", new
        {
            id_plano = plano,
            tipo_cobertura = cliente.Dependentes.Count == 0 ? "TITULAR" : "TITULAR_E_DEPENDENTES",
            dependentes = cliente.Dependentes,
            periodicidade = "MENSAL"
        }, JsonApi.Opcoes);
        return (await LerAsync(response, HttpStatusCode.Created))["id_contrato"]!.GetValue<Guid>();
    }

    internal static async Task<JsonObject> LerAsync(HttpResponseMessage response, HttpStatusCode esperado)
    {
        if (response.StatusCode != esperado)
        {
            throw new InvalidOperationException(
                $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: esperado {esperado}, veio {response.StatusCode}. "
                + await response.Content.ReadAsStringAsync());
        }

        return (await response.Content.ReadFromJsonAsync<JsonObject>(JsonApi.Opcoes))!;
    }
}

/// <summary>Atalhos de uma funerária dos testes: equipe, planos, itens do catálogo e clientes com dependentes.</summary>
public sealed class FunerariaDeTeste(FunerariaApiFactory factory, Guid tenant, string slug)
{
    public Guid Tenant { get; } = tenant;

    public string Slug { get; } = slug;

    public HttpClient Equipe(string perfil = Perfis.Funeraria) => factory.Http(Slug, TokenDeTeste.Gerar(perfil, Tenant));

    public async Task<Guid> CriarPlanoAsync(int limiteDependentes = 4, bool permiteSomenteDependente = false, bool ativo = true, int carenciaDias = 90)
    {
        HttpResponseMessage criado = await Equipe().PostAsJsonAsync("/api/v1/gestao/planos", new
        {
            nome = $"Plano {Guid.NewGuid():N}"[..20],
            descricao = "Plano de teste",
            valor_mensal = 89.90m,
            valor_anual = 970.92m,
            valor_dependente_mensal = 19.90m,
            valor_dependente_anual = 214.92m,
            carencia_dias = carenciaDias,
            limite_dependentes = limiteDependentes,
            permite_somente_dependente = permiteSomenteDependente
        }, JsonApi.Opcoes);
        Guid id = (await Cenarios.LerAsync(criado, HttpStatusCode.Created))["id_plano"]!.GetValue<Guid>();

        if (!ativo)
        {
            await Equipe().PatchAsJsonAsync($"/api/v1/gestao/planos/{id}/status", new { ativo = false }, JsonApi.Opcoes);
        }

        return id;
    }

    /// <summary>Cria uma categoria e um item ativo no catálogo (FUN-01).</summary>
    public async Task<Guid> CriarItemCatalogoAsync(string nome, decimal preco = 100m)
    {
        HttpResponseMessage categoria = await Equipe().PostAsJsonAsync("/api/v1/gestao/catalogo/categorias", new { nome = $"Cat {Guid.NewGuid():N}"[..12] }, JsonApi.Opcoes);
        Guid idCategoria = (await Cenarios.LerAsync(categoria, HttpStatusCode.Created))["id_categoria"]!.GetValue<Guid>();
        HttpResponseMessage item = await Equipe().PostAsJsonAsync("/api/v1/gestao/catalogo", new { id_categoria = idCategoria, nome, preco }, JsonApi.Opcoes);
        return (await Cenarios.LerAsync(item, HttpStatusCode.Created))["id_item"]!.GetValue<Guid>();
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
            senha = FunerariaApiFactory.SenhaPadrao,
            endereco = "Rua Exemplo, 100",
            cep = "87000-000",
            cidade = "Maringá",
            estado = "PR"
        }, JsonApi.Opcoes);
        Guid idCliente = (await Cenarios.LerAsync(cadastro, HttpStatusCode.Created))["id_cliente"]!.GetValue<Guid>();

        HttpResponseMessage login = await factory.Http(Slug).PostAsJsonAsync("/api/v1/auth/login", new { email, senha = FunerariaApiFactory.SenhaPadrao }, JsonApi.Opcoes);
        string token = (await Cenarios.LerAsync(login, HttpStatusCode.OK))["access_token"]!.GetValue<string>();
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
            }, JsonApi.Opcoes);
            ids.Add((await Cenarios.LerAsync(dependente, HttpStatusCode.Created))["id_dependente"]!.GetValue<Guid>());
        }

        return new ClienteDeTeste(idCliente, http, ids);
    }
}

public sealed record ClienteDeTeste(Guid IdCliente, HttpClient Http, IReadOnlyList<Guid> Dependentes);
