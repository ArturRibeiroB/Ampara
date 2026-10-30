using System.Data.Common;
using System.Text.Json;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Identidade.Application.Auditoria;

/// <summary>
/// Consulta da auditoria (AUT-09). O gestor vê tudo o que aconteceu na funerária dele, inclusive o que o ADMIN fez nela
/// (acesso de suporte fica visível). O ADMIN vê as ações feitas no nível da plataforma (DEC-06).
/// </summary>
public sealed record ConsultarAuditoriaQuery(
    PaginaRequest Pagina,
    bool DaPlataforma,
    string? Acao,
    string? Entidade,
    Guid? IdEntidade,
    Guid? IdUsuario,
    DateTime? De,
    DateTime? Ate)
    : IQuery<PaginaResult<RegistroAuditoriaResponse>>;

/// <param name="IdAuditoria">Registro da auditoria.</param>
/// <param name="DataHora">Quando (UTC).</param>
/// <param name="Acao">O que foi feito, ex.: CONTRATO_CRIADO.</param>
/// <param name="Entidade">Tipo do registro afetado.</param>
/// <param name="IdEntidade">Registro afetado.</param>
/// <param name="IdUsuario">Quem fez.</param>
/// <param name="NomeUsuario">Nome atual de quem fez.</param>
/// <param name="Ip">IP de origem da requisição.</param>
/// <param name="ValoresAnteriores">Estado antes da mudança.</param>
/// <param name="ValoresNovos">Estado depois da mudança.</param>
public sealed record RegistroAuditoriaResponse(
    Guid IdAuditoria,
    DateTime DataHora,
    string Acao,
    string Entidade,
    Guid? IdEntidade,
    Guid? IdUsuario,
    string? NomeUsuario,
    string? Ip,
    JsonElement? ValoresAnteriores,
    JsonElement? ValoresNovos);

public sealed class ConsultarAuditoriaQueryHandler(IDbConnectionFactory connectionFactory, ITenantContext tenantContext)
    : IQueryHandler<ConsultarAuditoriaQuery, PaginaResult<RegistroAuditoriaResponse>>
{
    // A auditoria não usa o filtro global: o escopo (funerária ou plataforma) vai explícito no WHERE.
    private const string Sql =
        """
        SELECT a.id_auditoria             AS IdAuditoria,
               a.data_hora                AS DataHora,
               a.acao                     AS Acao,
               a.entidade                 AS Entidade,
               a.id_entidade              AS IdEntidade,
               a.id_usuario               AS IdUsuario,
               u.nome                     AS NomeUsuario,
               a.ip                       AS Ip,
               a.valores_anteriores::text AS ValoresAnteriores,
               a.valores_novos::text      AS ValoresNovos,
               count(*) OVER ()           AS Total
        FROM identidade.auditoria a
        LEFT JOIN identidade.usuarios u ON u.id_usuario = a.id_usuario
        WHERE ((@DaPlataforma AND a.plataforma) OR (NOT @DaPlataforma AND a.id_tenant = @IdTenant))
          AND (@Acao::text IS NULL OR a.acao = @Acao)
          AND (@Entidade::text IS NULL OR a.entidade = @Entidade)
          AND (@IdEntidade::uuid IS NULL OR a.id_entidade = @IdEntidade)
          AND (@IdUsuario::uuid IS NULL OR a.id_usuario = @IdUsuario)
          AND (@De::timestamptz IS NULL OR a.data_hora >= @De)
          AND (@Ate::timestamptz IS NULL OR a.data_hora <= @Ate)
        ORDER BY a.data_hora DESC, a.id_auditoria DESC
        LIMIT @Tamanho OFFSET @Deslocamento
        """;

    public async ValueTask<Result<PaginaResult<RegistroAuditoriaResponse>>> Handle(ConsultarAuditoriaQuery query, CancellationToken cancellationToken)
    {
        PaginaRequest pagina = query.Pagina.Normalizada();
        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        List<Linha> linhas = (await connection.QueryAsync<Linha>(new CommandDefinition(
            Sql,
            new
            {
                query.DaPlataforma,
                IdTenant = query.DaPlataforma ? (Guid?)null : tenantContext.RequiredTenantId,
                Acao = query.Acao?.Trim().ToUpperInvariant(),
                query.Entidade,
                query.IdEntidade,
                query.IdUsuario,
                query.De,
                query.Ate,
                pagina.Tamanho,
                pagina.Deslocamento
            },
            cancellationToken: cancellationToken))).ToList();

        long total = linhas.Count > 0 ? linhas[0].Total : 0;
        return new PaginaResult<RegistroAuditoriaResponse>([.. linhas.Select(l => l.Resposta)], pagina.Pagina, pagina.Tamanho, total);
    }

    private sealed record Linha
    {
        public Guid IdAuditoria { get; init; }

        public DateTime DataHora { get; init; }

        public required string Acao { get; init; }

        public required string Entidade { get; init; }

        public Guid? IdEntidade { get; init; }

        public Guid? IdUsuario { get; init; }

        public string? NomeUsuario { get; init; }

        public string? Ip { get; init; }

        public string? ValoresAnteriores { get; init; }

        public string? ValoresNovos { get; init; }

        public long Total { get; init; }

        public RegistroAuditoriaResponse Resposta => new(
            IdAuditoria, DateTime.SpecifyKind(DataHora, DateTimeKind.Utc), Acao, Entidade, IdEntidade, IdUsuario, NomeUsuario, Ip,
            Elemento(ValoresAnteriores), Elemento(ValoresNovos));

        private static JsonElement? Elemento(string? json)
        {
            if (json is null)
            {
                return null;
            }

            using var documento = JsonDocument.Parse(json);
            return documento.RootElement.Clone();
        }
    }
}
