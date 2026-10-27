using System.Data.Common;
using Dapper;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Clientes.Application.Queries.Gestao;

/// <summary>
/// Busca de clientes pela equipe da funerária (CLI-03), por nome (parte do nome) ou CPF, com paginação (INF-17).
/// O CPF é cifrado no banco (SEG-01): a busca usa o hash e a listagem mostra o CPF mascarado.
/// O filtro por status do contrato entra quando o módulo Contratos existir.
/// </summary>
public sealed record BuscarClientesQuery(PaginaRequest Pagina, string? Nome, string? Cpf, OrdenacaoRequest? Ordenacao = null)
    : IQuery<PaginaResult<ClienteResumoResponse>>;

public sealed record ClienteResumoResponse
{
    public Guid IdCliente { get; init; }

    public required string Nome { get; init; }

    /// <summary>Mascarado (***.456.789-**): a listagem não precisa do CPF completo (SEG-01).</summary>
    public required string Cpf { get; init; }

    public required string Email { get; init; }

    public string? Telefone { get; init; }

    public string? Cidade { get; init; }

    public string? Estado { get; init; }

    public int QuantidadeDependentes { get; init; }
}

public sealed class BuscarClientesQueryHandler(
    IDbConnectionFactory connectionFactory,
    ITenantContext tenantContext,
    ICriptografiaDados criptografia)
    : IQueryHandler<BuscarClientesQuery, PaginaResult<ClienteResumoResponse>>
{
    // Dapper não passa pelo filtro global do EF Core: o id_tenant é obrigatório no WHERE (TEN-01).
    private const string Sql =
        """
        SELECT c.id_cliente        AS IdCliente,
               c.nome              AS Nome,
               c.cpf               AS Cpf,
               c.email             AS Email,
               c.telefone          AS Telefone,
               c.cidade            AS Cidade,
               c.estado            AS Estado,
               (SELECT count(*) FROM clientes.dependentes d
                 WHERE d.id_cliente = c.id_cliente AND d.removido_em IS NULL)::int AS QuantidadeDependentes,
               count(*) OVER ()    AS Total
        FROM clientes.clientes c
        WHERE c.id_tenant = @IdTenant
          AND (@Nome IS NULL OR c.nome ILIKE @Nome)
          AND (@CpfHash::text IS NULL OR c.cpf_hash = @CpfHash)
        /*ORDENACAO*/
        LIMIT @Tamanho OFFSET @Deslocamento
        """;

    private static readonly OrdenacaoPermitida Ordenacoes = new(
        new Dictionary<string, string>
        {
            ["nome"] = "c.nome",
            ["cidade"] = "c.cidade",
            ["cadastrado_em"] = "c.created_at"
        },
        padrao: "nome",
        desempate: "c.id_cliente");

    public async ValueTask<Result<PaginaResult<ClienteResumoResponse>>> Handle(BuscarClientesQuery query, CancellationToken cancellationToken)
    {
        Result<string> orderBy = Ordenacoes.OrderBy(query.Ordenacao);
        if (orderBy.IsFailure)
        {
            return Result.Failure<PaginaResult<ClienteResumoResponse>>(orderBy.Error);
        }

        PaginaRequest pagina = query.Pagina.Normalizada();
        string? nome = string.IsNullOrWhiteSpace(query.Nome)
            ? null
            : $"%{query.Nome.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
        string? cpf = string.IsNullOrWhiteSpace(query.Cpf) ? null : DocumentosBrasileiros.Normalizar(query.Cpf);

        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        List<Linha> linhas = (await connection.QueryAsync<Linha>(new CommandDefinition(
            Sql.Replace("/*ORDENACAO*/", orderBy.Value, StringComparison.Ordinal),
            new { IdTenant = tenantContext.RequiredTenantId, Nome = nome, CpfHash = cpf is null ? null : criptografia.Hash(cpf), pagina.Tamanho, pagina.Deslocamento },
            cancellationToken: cancellationToken))).ToList();

        long total = linhas.Count > 0 ? linhas[0].Total : 0;
        return new PaginaResult<ClienteResumoResponse>(
            linhas.Select(l => l.Resumo(criptografia)).ToList(), pagina.Pagina, pagina.Tamanho, total);
    }

    private sealed record Linha
    {
        public Guid IdCliente { get; init; }

        public required string Nome { get; init; }

        public required string Cpf { get; init; }

        public required string Email { get; init; }

        public string? Telefone { get; init; }

        public string? Cidade { get; init; }

        public string? Estado { get; init; }

        public int QuantidadeDependentes { get; init; }

        public long Total { get; init; }

        public ClienteResumoResponse Resumo(ICriptografiaDados criptografia) => new()
        {
            IdCliente = IdCliente,
            Nome = Nome,
            Cpf = Mascaras.Cpf(criptografia.Decifrar(Cpf).Trim()),
            Email = Email,
            Telefone = Telefone,
            Cidade = Cidade,
            Estado = Estado,
            QuantidadeDependentes = QuantidadeDependentes
        };
    }
}
