using System.Data.Common;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Clientes.Infrastructure.Database;

/// <summary>
/// Na subida da API, cifra os CPFs gravados antes do SEG-01 (texto puro, sem hash). Roda depois das migrations e não faz
/// nada quando todos já estão cifrados. Até terminar, o CPF antigo continua legível (a decifragem aceita texto puro).
/// </summary>
internal sealed class CifrarCpfsLegados(
    IDbConnectionFactory conexoes,
    ICriptografiaDados criptografia,
    ILogger<CifrarCpfsLegados> logger)
    : IHostedService
{
    private static readonly (string Tabela, string Chave)[] Tabelas = [("clientes.clientes", "id_cliente"), ("clientes.dependentes", "id_dependente")];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using DbConnection conexao = await conexoes.OpenConnectionAsync(cancellationToken);
            foreach ((string tabela, string chave) in Tabelas)
            {
                List<(Guid Id, string Cpf)> legados = [.. await conexao.QueryAsync<(Guid, string)>(new CommandDefinition(
                    $"SELECT {chave}, cpf FROM {tabela} WHERE cpf_hash IS NULL", cancellationToken: cancellationToken))];

                foreach ((Guid id, string cpf) in legados)
                {
                    string puro = criptografia.Decifrar(cpf).Trim();
                    await conexao.ExecuteAsync(new CommandDefinition(
                        $"UPDATE {tabela} SET cpf = @Cpf, cpf_hash = @Hash WHERE {chave} = @Id",
                        new { Cpf = criptografia.Cifrar(puro), Hash = criptografia.Hash(puro), Id = id },
                        cancellationToken: cancellationToken));
                }

                if (legados.Count > 0)
                {
                    logger.LogInformation("{Quantidade} CPFs de {Tabela} foram cifrados (SEG-01).", legados.Count, tabela);
                }
            }
        }
        catch (DbException e)
        {
            logger.LogError(e, "Não foi possível cifrar os CPFs antigos. A API segue lendo os que estão em texto puro.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
