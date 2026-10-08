using System.Data.Common;
using System.Text.Json;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Serialization;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.Application.Queries.ObterPlano;

public sealed class ObterPlanoQueryHandler(IDbConnectionFactory connectionFactory, ITenantContext tenantContext)
    : IQueryHandler<ObterPlanoQuery, PlanoResponse>
{
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
               permite_somente_dependente AS PermiteSomenteDependente,
               configuracoes::text        AS Configuracoes,
               ativo                      AS Ativo
        FROM planos.planos
        WHERE id_tenant = @IdTenant
          AND id_plano = @IdPlano
          AND (ativo OR NOT @SomenteAtivos)
        """;

    public async ValueTask<Result<PlanoResponse>> Handle(ObterPlanoQuery query, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        PlanoRow? row = await connection.QuerySingleOrDefaultAsync<PlanoRow>(new CommandDefinition(
            Sql,
            new { IdTenant = tenantContext.RequiredTenantId, query.IdPlano, query.SomenteAtivos },
            cancellationToken: cancellationToken));

        if (row is null)
        {
            return PlanoErros.NaoEncontrado;
        }

        PlanoConfiguracoes configuracoes =
            JsonSerializer.Deserialize<PlanoConfiguracoes>(row.Configuracoes, JsonbOptions.Default) ?? PlanoConfiguracoes.Padrao;

        return new PlanoResponse(
            row.IdPlano,
            row.Nome,
            row.Descricao,
            row.ValorMensal,
            row.ValorAnual,
            row.ValorDependenteMensal,
            row.ValorDependenteAnual,
            row.CarenciaDias,
            row.LimiteDependentes,
            row.PermiteSomenteDependente,
            configuracoes,
            row.Ativo);
    }

    private sealed record PlanoRow
    {
        public Guid IdPlano { get; init; }

        public required string Nome { get; init; }

        public string? Descricao { get; init; }

        public decimal ValorMensal { get; init; }

        public decimal ValorAnual { get; init; }

        public decimal ValorDependenteMensal { get; init; }

        public decimal ValorDependenteAnual { get; init; }

        public int CarenciaDias { get; init; }

        public int LimiteDependentes { get; init; }

        public bool PermiteSomenteDependente { get; init; }

        public required string Configuracoes { get; init; }

        public bool Ativo { get; init; }
    }
}
