using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Funeraria.Modules.Identidade.IntegrationTests.SessaoDeTeste;

namespace Funeraria.Modules.Identidade.IntegrationTests;

/// <summary>SEG-01: CPF de funcionários e consultores cifrado em repouso, com unicidade pelo hash.</summary>
[Collection(ApiCollection.Nome)]
public sealed class CpfFuncionarioCifradoTests(IdentidadeApiFactory factory)
{
    [Fact]
    public async Task Cpf_do_funcionario_fica_cifrado_no_banco_e_a_listagem_da_equipe_mostra_o_numero()
    {
        (HttpClient gestor, _) = await GestorAsync();
        string cpf = DocumentosDeTeste.NovoCpf();

        HttpResponseMessage criado = await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(cpf), Json, Ct);

        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        JsonObject membro = (await criado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!;
        Assert.Equal(cpf, membro["cpf"]!.GetValue<string>());

        (string gravado, string hash) = await LerAsync(membro["id_funcionario"]!.GetValue<Guid>());
        Assert.StartsWith("dp1:", gravado, StringComparison.Ordinal);
        Assert.DoesNotContain(cpf, gravado, StringComparison.Ordinal);
        Assert.Equal(factory.Services.GetRequiredService<ICriptografiaDados>().Hash(cpf), hash);

        JsonObject lista = (await gestor.GetFromJsonAsync<JsonObject>("/api/v1/gestao/usuarios?perfil=FUNCIONARIO", Json, Ct))!;
        Assert.Equal(cpf, Assert.Single(lista["itens"]!.AsArray())!["cpf"]!.GetValue<string>());
    }

    [Fact]
    public async Task Cpf_gravado_antes_da_cifra_e_cifrado_na_subida_e_continua_unico_na_funeraria()
    {
        (HttpClient gestor, _) = await GestorAsync();
        string cpf = DocumentosDeTeste.NovoCpf();
        HttpResponseMessage criado = await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(cpf), Json, Ct);
        Guid id = (await criado.Content.ReadFromJsonAsync<JsonObject>(Json, Ct))!["id_funcionario"]!.GetValue<Guid>();
        await ExecutarAsync("UPDATE identidade.funcionarios SET cpf = @cpf, cpf_hash = NULL WHERE id_funcionario = @id", new { cpf, id });

        IHostedService migracao = factory.Services.GetServices<IHostedService>()
            .Single(s => s.GetType().FullName == "Funeraria.Modules.Identidade.Infrastructure.Equipe.CifrarCpfsFuncionariosLegados");
        await migracao.StartAsync(Ct);

        (string gravado, string hash) = await LerAsync(id);
        Assert.StartsWith("dp1:", gravado, StringComparison.Ordinal);
        Assert.Equal(factory.Services.GetRequiredService<ICriptografiaDados>().Hash(cpf), hash);
        JsonObject repetido = await (await gestor.PostAsJsonAsync("/api/v1/gestao/funcionarios", NovoFuncionario(cpf), Json, Ct))
            .ProblemaAsync(HttpStatusCode.Conflict);
        Assert.Equal("CPF já cadastrado nesta funerária.", repetido["detail"]!.GetValue<string>());
    }

    private async Task<(HttpClient Gestor, Guid Tenant)> GestorAsync()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        (_, string email) = await factory.CriarUsuarioAsync((tenant, PerfilVinculo.Funeraria));
        (HttpClient http, _, _) = await factory.EntrarAsync(slug, email);
        return (http, tenant);
    }

    private async Task<(string Cpf, string Hash)> LerAsync(Guid id)
    {
        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        return await conexao.QuerySingleAsync<(string, string)>(new CommandDefinition(
            "SELECT cpf, cpf_hash FROM identidade.funcionarios WHERE id_funcionario = @id", new { id }, cancellationToken: Ct));
    }

    private async Task ExecutarAsync(string sql, object parametros)
    {
        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        await conexao.ExecuteAsync(new CommandDefinition(sql, parametros, cancellationToken: Ct));
    }

    private static object NovoFuncionario(string cpf) => new
    {
        nome = "Maria Souza",
        cpf,
        email = $"equipe-{Guid.NewGuid():N}@teste.localhost",
        telefone = "(44) 99999-0000",
        perfil = "FUNCIONARIO"
    };
}
