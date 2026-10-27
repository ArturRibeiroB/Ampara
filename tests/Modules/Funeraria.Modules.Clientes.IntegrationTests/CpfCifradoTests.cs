using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Funeraria.Modules.Clientes.IntegrationTests.ApiCliente;

namespace Funeraria.Modules.Clientes.IntegrationTests;

/// <summary>SEG-01: CPF cifrado em repouso, busca pelo hash e CPF mascarado na listagem.</summary>
[Collection(ApiCollection.Nome)]
public sealed class CpfCifradoTests(ClientesApiFactory factory)
{
    [Fact]
    public async Task Cpf_fica_cifrado_no_banco_e_a_api_continua_mostrando_o_cpf_ao_dono()
    {
        (_, string slug) = await factory.CriarFunerariaAsync();
        Dictionary<string, object?> corpo = NovoCliente();
        string cpf = (string)corpo["cpf"]!;
        Guid id = (await factory.CadastrarAsync(slug, corpo))["id_cliente"]!.GetValue<Guid>();

        (string gravado, string hash) = await LerAsync(id);

        Assert.StartsWith("dp1:", gravado, StringComparison.Ordinal);
        Assert.DoesNotContain(cpf, gravado, StringComparison.Ordinal);
        Assert.Equal(64, hash.Length);

        string token = await factory.LoginAsync(slug, (string)corpo["email"]!);
        JsonObject me = (await factory.Cliente(slug, token).GetFromJsonAsync<JsonObject>("/api/v1/clientes/me", Json, Ct))!;
        Assert.Equal(cpf, me["cpf"]!.GetValue<string>());
    }

    [Fact]
    public async Task Listagem_da_equipe_mostra_o_cpf_mascarado_e_o_detalhe_mostra_completo()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        Dictionary<string, object?> corpo = NovoCliente();
        string cpf = (string)corpo["cpf"]!;
        Guid id = (await factory.CadastrarAsync(slug, corpo))["id_cliente"]!.GetValue<Guid>();
        HttpClient equipe = factory.Cliente(slug, TokenDeTeste.Gerar("FUNCIONARIO", tenant));

        JsonObject lista = (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/gestao/clientes?cpf={cpf}", Json, Ct))!;
        JsonObject detalhe = (await equipe.GetFromJsonAsync<JsonObject>($"/api/v1/gestao/clientes/{id}", Json, Ct))!;

        Assert.Equal($"***.{cpf[3..6]}.{cpf[6..9]}-**", Assert.Single(lista["itens"]!.AsArray())!["cpf"]!.GetValue<string>());
        Assert.Equal(cpf, detalhe["cliente"]!["cpf"]!.GetValue<string>());
    }

    [Fact]
    public async Task Cpf_gravado_antes_da_cifra_e_cifrado_na_subida_e_volta_a_ser_encontrado()
    {
        (Guid tenant, string slug) = await factory.CriarFunerariaAsync();
        Dictionary<string, object?> corpo = NovoCliente();
        string cpf = (string)corpo["cpf"]!;
        Guid id = (await factory.CadastrarAsync(slug, corpo))["id_cliente"]!.GetValue<Guid>();
        await ExecutarAsync("UPDATE clientes.clientes SET cpf = @cpf, cpf_hash = NULL WHERE id_cliente = @id", new { cpf, id });

        IHostedService migracao = factory.Services.GetServices<IHostedService>().Single(s => s.GetType().Name == "CifrarCpfsLegados");
        await migracao.StartAsync(Ct);

        (string gravado, string hash) = await LerAsync(id);
        Assert.StartsWith("dp1:", gravado, StringComparison.Ordinal);
        Assert.Equal(factory.Services.GetRequiredService<ICriptografiaDados>().Hash(cpf), hash);
        JsonObject achado = (await factory.Cliente(slug, TokenDeTeste.Gerar("FUNCIONARIO", tenant))
            .GetFromJsonAsync<JsonObject>($"/api/v1/gestao/clientes?cpf={cpf}", Json, Ct))!;
        Assert.Equal(1, achado["total"]!.GetValue<int>());
    }

    private async Task<(string Cpf, string Hash)> LerAsync(Guid id)
    {
        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        return await conexao.QuerySingleAsync<(string, string)>(new CommandDefinition(
            "SELECT cpf, cpf_hash FROM clientes.clientes WHERE id_cliente = @id", new { id }, cancellationToken: Ct));
    }

    private async Task ExecutarAsync(string sql, object parametros)
    {
        await using DbConnection conexao = await factory.Services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync(Ct);
        await conexao.ExecuteAsync(new CommandDefinition(sql, parametros, cancellationToken: Ct));
    }
}
