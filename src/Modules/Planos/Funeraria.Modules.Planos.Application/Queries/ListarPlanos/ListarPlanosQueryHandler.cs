using System.Data.Common;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Planos.Application.Queries.ListarPlanos;

/// <remarks>A lista dos ativos, que é a pública, fica em cache por funerária (INF-19); a do gestor é sempre lida do banco.</remarks>
public sealed class ListarPlanosQueryHandler(IDbConnectionFactory connectionFactory, ITenantContext tenantContext, ICacheDaFuneraria cache)
    : IQueryHandler<ListarPlanosQuery, IReadOnlyList<PlanoResumoResponse>>
{
    // Dapper não passa pelo filtro global do EF Core: o id_tenant é obrigatório no WHERE (TEN-01).
    private const string Sql =
        """
        SELECT id_plano                   AS IdPlano,
               nome                       AS Nome,
               descricao                  AS Descricao,
               valor_mensal               AS ValorMensal,
               valor_anual                AS ValorAnual,
               valor_dependente_mensal    AS ValorDependenteMensal,
               valor_dependente_anual     AS ValorDependenteAnual,
               carencia_dias              AS CarenciaDias,
               limite_dependentes         AS LimiteDependentes,
               ativo                      AS Ativo
        FROM planos.planos
        WHERE id_tenant = @IdTenant
          AND (ativo OR NOT @SomenteAtivos)
        ORDER BY nome
        """;

    public async ValueTask<Result<IReadOnlyList<PlanoResumoResponse>>> Handle(
        ListarPlanosQuery query,
        CancellationToken cancellationToken) =>
        query.SomenteAtivos
            ? await cache.ObterAsync(AreasCache.Planos, "ativos", ct => CarregarAsync(somenteAtivos: true, ct), cancellationToken)
            : await CarregarAsync(somenteAtivos: false, cancellationToken);

    private async ValueTask<List<PlanoResumoResponse>> CarregarAsync(bool somenteAtivos, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        IEnumerable<PlanoResumoResponse> planos = await connection.QueryAsync<PlanoResumoResponse>(new CommandDefinition(
            Sql,
            new { IdTenant = tenantContext.RequiredTenantId, SomenteAtivos = somenteAtivos },
            cancellationToken: cancellationToken));

        return planos.ToList();
    }
}
