using System.Data.Common;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Funeraria.BuildingBlocks.Infrastructure.Seguranca;

/// <summary>Coluna com dado pessoal cifrado em repouso e a coluna do hash usado em busca e unicidade (SEG-01).</summary>
public sealed record ColunaCifrada(string Tabela, string Chave, string Coluna, string ColunaHash);

/// <summary>
/// Na subida da API, cifra os valores gravados antes da cifragem (texto puro, sem hash). Roda depois das migrations e não
/// faz nada quando todos já estão cifrados. Até terminar, o valor antigo continua legível (a decifragem aceita texto puro).
/// Cada módulo herda com as próprias colunas: só toca o schema dele (INF-01).
/// </summary>
public abstract class CifrarDadosLegados(IDbConnectionFactory conexoes, ICriptografiaDados criptografia, ILogger logger)
    : IHostedService
{
    protected abstract IReadOnlyList<ColunaCifrada> Colunas { get; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using DbConnection conexao = await conexoes.OpenConnectionAsync(cancellationToken);
            foreach (ColunaCifrada c in Colunas)
            {
                List<(Guid Id, string Valor)> legados = [.. await conexao.QueryAsync<(Guid, string)>(new CommandDefinition(
                    $"SELECT {c.Chave}, {c.Coluna} FROM {c.Tabela} WHERE {c.ColunaHash} IS NULL", cancellationToken: cancellationToken))];

                foreach ((Guid id, string valor) in legados)
                {
                    string puro = criptografia.Decifrar(valor).Trim();
                    await conexao.ExecuteAsync(new CommandDefinition(
                        $"UPDATE {c.Tabela} SET {c.Coluna} = @Valor, {c.ColunaHash} = @Hash WHERE {c.Chave} = @Id",
                        new { Valor = criptografia.Cifrar(puro), Hash = criptografia.Hash(puro), Id = id },
                        cancellationToken: cancellationToken));
                }

                if (legados.Count > 0)
                {
                    logger.LogInformation("{Quantidade} valores de {Tabela}.{Coluna} foram cifrados (SEG-01).", legados.Count, c.Tabela, c.Coluna);
                }
            }
        }
        catch (DbException e)
        {
            logger.LogError(e, "Não foi possível cifrar os dados antigos. A API segue lendo os que estão em texto puro.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
