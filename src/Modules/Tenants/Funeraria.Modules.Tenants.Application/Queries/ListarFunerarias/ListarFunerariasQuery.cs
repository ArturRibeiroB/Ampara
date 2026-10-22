using System.Data.Common;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Tenants.Domain.Funerarias;

namespace Funeraria.Modules.Tenants.Application.Queries.ListarFunerarias;

/// <summary>Funerárias da plataforma para o ADMIN (TEN-02), com busca, filtro de status e paginação (INF-17).</summary>
public sealed record ListarFunerariasQuery(PaginaRequest Pagina, string? Busca, StatusFuneraria? Status)
    : IQuery<PaginaResult<FunerariaListaResponse>>;

public sealed record FunerariaListaResponse
{
    public Guid IdTenant { get; init; }

    public required string NomeFantasia { get; init; }

    public required string RazaoSocial { get; init; }

    public required string Cnpj { get; init; }

    public required string Slug { get; init; }

    public required string EmailContato { get; init; }

    public StatusFuneraria Status { get; init; }

    public OrigemCadastro OrigemCadastro { get; init; }

    public DateTime CriadaEm { get; init; }
}

/// <summary>
/// Lê a tabela da própria plataforma (tenants.funerarias). Não há filtro de tenant porque o resultado é a lista
/// de tenants; o endpoint é restrito ao ADMIN.
/// </summary>
public sealed class ListarFunerariasQueryHandler(IDbConnectionFactory connectionFactory)
    : IQueryHandler<ListarFunerariasQuery, PaginaResult<FunerariaListaResponse>>
{
    private const string Sql =
        """
        SELECT id_tenant          AS IdTenant,
               nome_fantasia      AS NomeFantasia,
               razao_social       AS RazaoSocial,
               cnpj               AS Cnpj,
               slug               AS Slug,
               email_contato      AS EmailContato,
               status             AS Status,
               origem_cadastro    AS OrigemCadastro,
               created_at         AS CriadaEm,
               count(*) OVER ()   AS Total
        FROM tenants.funerarias
        WHERE (@Busca IS NULL
               OR nome_fantasia ILIKE @Busca OR razao_social ILIKE @Busca OR slug ILIKE @Busca OR cnpj ILIKE @Busca)
          AND (@Status IS NULL OR status = @Status)
        ORDER BY nome_fantasia
        LIMIT @Tamanho OFFSET @Deslocamento
        """;

    public async ValueTask<Result<PaginaResult<FunerariaListaResponse>>> Handle(
        ListarFunerariasQuery query,
        CancellationToken cancellationToken)
    {
        PaginaRequest pagina = query.Pagina.Normalizada();
        string? busca = string.IsNullOrWhiteSpace(query.Busca) ? null : $"%{EscaparLike(query.Busca.Trim())}%";

        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        List<Linha> linhas = (await connection.QueryAsync<Linha>(new CommandDefinition(
            Sql,
            new { Busca = busca, Status = query.Status?.ToString(), pagina.Tamanho, pagina.Deslocamento },
            cancellationToken: cancellationToken))).ToList();

        long total = linhas.Count > 0 ? linhas[0].Total : 0;
        return new PaginaResult<FunerariaListaResponse>(
            linhas.Select(l => l.Funeraria).ToList(), pagina.Pagina, pagina.Tamanho, total);
    }

    // % e _ digitados na busca são literais, não curingas.
    private static string EscaparLike(string texto) =>
        texto.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    private sealed record Linha
    {
        public Guid IdTenant { get; init; }

        public required string NomeFantasia { get; init; }

        public required string RazaoSocial { get; init; }

        public required string Cnpj { get; init; }

        public required string Slug { get; init; }

        public required string EmailContato { get; init; }

        public required string Status { get; init; }

        public required string OrigemCadastro { get; init; }

        public DateTime CriadaEm { get; init; }

        public long Total { get; init; }

        public FunerariaListaResponse Funeraria => new()
        {
            IdTenant = IdTenant,
            NomeFantasia = NomeFantasia,
            RazaoSocial = RazaoSocial,
            Cnpj = Cnpj.Trim(),
            Slug = Slug,
            EmailContato = EmailContato,
            Status = Enum.Parse<StatusFuneraria>(Status),
            OrigemCadastro = Enum.Parse<OrigemCadastro>(OrigemCadastro),
            CriadaEm = CriadaEm
        };
    }
}
