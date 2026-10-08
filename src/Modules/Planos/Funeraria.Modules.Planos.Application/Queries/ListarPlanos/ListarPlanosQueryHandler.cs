using System.Data.Common;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Planos.Application.Queries.ListarPlanos;

public sealed class ListarPlanosQueryHandler(IDbConnectionFactory connectionFactory, ITenantContext tenantContext)
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
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        IEnumerable<PlanoResumoResponse> planos = await connection.QueryAsync<PlanoResumoResponse>(new CommandDefinition(
            Sql,
            new { IdTenant = tenantContext.RequiredTenantId, query.SomenteAtivos },
            cancellationToken: cancellationToken));

        return planos.ToList();
    }
}
