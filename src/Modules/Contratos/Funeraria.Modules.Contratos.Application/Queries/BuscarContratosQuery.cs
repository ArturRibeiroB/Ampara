using System.Data.Common;
using Dapper;
using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Contratos.Domain.Contratos;

namespace Funeraria.Modules.Contratos.Application.Queries;

/// <summary>
/// Busca de contratos pela equipe da funerária (CON-01): por status, plano e CPF do titular, com paginação e ordenação
/// (INF-17). O CPF é resolvido pelo módulo Clientes, sem ler as tabelas dele. Status: ATIVO, A_PAGAR, SUSPENSO ou CANCELADO.
/// </summary>
public sealed record BuscarContratosQuery(
    PaginaRequest Pagina,
    string? Status,
    Guid? IdPlano,
    string? Cpf,
    OrdenacaoRequest? Ordenacao = null)
    : IQuery<PaginaResult<ContratoResumoResponse>>;

internal sealed class BuscarContratosQueryValidator : AbstractValidator<BuscarContratosQuery>
{
    public BuscarContratosQueryValidator()
    {
        RuleFor(q => q.Status)
            .Must(s => s is null || StatusFiltro.Ler(s) is not null)
            .WithMessage("Status inválido. Use ATIVO, A_PAGAR, SUSPENSO ou CANCELADO.");
    }
}

internal static class StatusFiltro
{
    /// <summary>Aceita o formato da API (A_PAGAR) e o nome do enum (APagar).</summary>
    public static StatusContrato? Ler(string valor) =>
        Enum.TryParse(valor.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out StatusContrato status)
        && Enum.IsDefined(status)
            ? status
            : null;
}

public sealed class BuscarContratosQueryHandler(IDbConnectionFactory connectionFactory, ITenantContext tenantContext, IClientesApi clientes)
    : IQueryHandler<BuscarContratosQuery, PaginaResult<ContratoResumoResponse>>
{
    // Dapper não passa pelo filtro global do EF Core: o id_tenant é obrigatório no WHERE (TEN-01).
    private const string Sql =
        """
        SELECT c.id_contrato       AS IdContrato,
               c.id_cliente        AS IdCliente,
               c.id_plano          AS IdPlano,
               c.nome_plano        AS NomePlano,
               c.tipo_cobertura    AS TipoCobertura,
               c.periodicidade     AS Periodicidade,
               c.status            AS Status,
               c.valor_total       AS ValorTotal,
               c.data_contratacao  AS DataContratacao,
               (SELECT count(*) FROM contratos.pessoas_cobertas p
                 WHERE p.id_contrato = c.id_contrato AND p.data_remocao IS NULL)::int AS QuantidadePessoas,
               count(*) OVER ()    AS Total
        FROM contratos.contratos c
        WHERE c.id_tenant = @IdTenant
          AND (@Status::text IS NULL OR c.status = @Status)
          AND (@IdPlano::uuid IS NULL OR c.id_plano = @IdPlano)
          AND (@IdCliente::uuid IS NULL OR c.id_cliente = @IdCliente)
        /*ORDENACAO*/
        LIMIT @Tamanho OFFSET @Deslocamento
        """;

    private static readonly OrdenacaoPermitida Ordenacoes = new(
        new Dictionary<string, string>
        {
            ["data_contratacao"] = "c.data_contratacao",
            ["valor_total"] = "c.valor_total",
            ["status"] = "c.status",
            ["plano"] = "c.nome_plano"
        },
        padrao: "data_contratacao",
        desempate: "c.id_contrato");

    public async ValueTask<Result<PaginaResult<ContratoResumoResponse>>> Handle(BuscarContratosQuery query, CancellationToken cancellationToken)
    {
        // Sem ordenação pedida, os mais recentes primeiro.
        OrdenacaoRequest ordenacao = query.Ordenacao is { OrdenarPor: not null } or { Direcao: not null }
            ? query.Ordenacao
            : new OrdenacaoRequest(Direcao: "desc");
        Result<string> orderBy = Ordenacoes.OrderBy(ordenacao);
        if (orderBy.IsFailure)
        {
            return Result.Failure<PaginaResult<ContratoResumoResponse>>(orderBy.Error);
        }

        PaginaRequest pagina = query.Pagina.Normalizada();

        Guid? idCliente = null;
        if (!string.IsNullOrWhiteSpace(query.Cpf))
        {
            TitularDto? titular = await clientes.ObterTitularPorCpfAsync(DocumentosBrasileiros.Normalizar(query.Cpf), cancellationToken);
            if (titular is null)
            {
                return new PaginaResult<ContratoResumoResponse>([], pagina.Pagina, pagina.Tamanho, 0);
            }

            idCliente = titular.IdCliente;
        }

        await using DbConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        List<Linha> linhas = (await connection.QueryAsync<Linha>(new CommandDefinition(
            Sql.Replace("/*ORDENACAO*/", orderBy.Value, StringComparison.Ordinal),
            new
            {
                IdTenant = tenantContext.RequiredTenantId,
                Status = query.Status is null ? null : StatusFiltro.Ler(query.Status)?.ToString(),
                query.IdPlano,
                IdCliente = idCliente,
                pagina.Tamanho,
                pagina.Deslocamento
            },
            cancellationToken: cancellationToken))).ToList();

        Dictionary<Guid, string> nomes = (await clientes.ObterTitularesAsync([.. linhas.Select(l => l.IdCliente).Distinct()], cancellationToken))
            .ToDictionary(t => t.IdCliente, t => t.Nome);

        long total = linhas.Count > 0 ? linhas[0].Total : 0;
        return new PaginaResult<ContratoResumoResponse>(
            [.. linhas.Select(l => l.Resumo(nomes.GetValueOrDefault(l.IdCliente, string.Empty)))], pagina.Pagina, pagina.Tamanho, total);
    }

    private sealed record Linha
    {
        public Guid IdContrato { get; init; }

        public Guid IdCliente { get; init; }

        public Guid IdPlano { get; init; }

        public required string NomePlano { get; init; }

        public required string TipoCobertura { get; init; }

        public required string Periodicidade { get; init; }

        public required string Status { get; init; }

        public decimal ValorTotal { get; init; }

        public DateTime DataContratacao { get; init; }

        public int QuantidadePessoas { get; init; }

        public long Total { get; init; }

        public ContratoResumoResponse Resumo(string nomeTitular) => new(
            IdContrato,
            IdCliente,
            nomeTitular,
            IdPlano,
            NomePlano,
            Enum.Parse<TipoCobertura>(TipoCobertura),
            Enum.Parse<Periodicidade>(Periodicidade),
            Enum.Parse<StatusContrato>(Status),
            ValorTotal,
            DateOnly.FromDateTime(DataContratacao),
            QuantidadePessoas);
    }
}
