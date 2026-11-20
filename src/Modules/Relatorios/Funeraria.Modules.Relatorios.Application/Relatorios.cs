using System.Data.Common;
using System.Globalization;
using System.Text;
using Dapper;
using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Relatorios.Application;

/// <summary>Cache de curta duração dos indicadores (REL-01). A implementação fica na infraestrutura.</summary>
public interface ICacheDeRelatorios
{
    Task<T> ObterAsync<T>(string chave, TimeSpan validade, Func<Task<T>> gerar);
}

public interface IParametrosPlataforma
{
    /// <summary>Taxa retida em cada pagamento (DEC-09).</summary>
    decimal TaxaPlataformaPercentual { get; }
}

/// <summary>Indicadores do painel da funerária (REL-01). Todo SQL daqui lê só as views do schema relatorios (INF-01).</summary>
public sealed record DashboardQuery : IQuery<DashboardResponse>;

/// <param name="ContratosAtivos">Contratos com status ATIVO.</param>
/// <param name="NovosContratosNoMes">Contratados neste mês, em qualquer status.</param>
/// <param name="TaxaInadimplencia">Dos contratos em vigor (ativos e suspensos), % com fatura do plano vencida.</param>
/// <param name="ReceitaPrevistaNoMes">Faturas com vencimento neste mês, menos as canceladas.</param>
/// <param name="ReceitaRecebidaNoMes">Faturas pagas neste mês, de qualquer vencimento.</param>
/// <param name="FuneraisEmAndamento">Funerais nem concluídos nem cancelados.</param>
/// <param name="TransladosEmAndamento">Translados nem entregues nem cancelados.</param>
/// <param name="GeradoEm">Quando os números foram calculados. Ficam em cache por um minuto.</param>
public sealed record DashboardResponse(
    long ContratosAtivos,
    long NovosContratosNoMes,
    decimal TaxaInadimplencia,
    decimal ReceitaPrevistaNoMes,
    decimal ReceitaRecebidaNoMes,
    long FuneraisEmAndamento,
    long TransladosEmAndamento,
    DateTime GeradoEm);

/// <summary>Indicadores da plataforma (REL-03), sem dados pessoais. É a única consulta daqui que atravessa funerárias.</summary>
public sealed record DashboardPlataformaQuery : IQuery<DashboardPlataformaResponse>;

/// <param name="FunerariasPorStatus">Quantas funerárias em cada status.</param>
/// <param name="ContratosAtivos">Contratos ativos em todas as funerárias.</param>
/// <param name="VolumePagoNoMes">Soma das faturas pagas neste mês.</param>
/// <param name="ReceitaPlataformaNoMes">Estimativa da taxa sobre os pagamentos do mês pelo Stripe. Baixa manual não paga taxa.</param>
/// <param name="FunerariasComPendenciaStripe">Funerárias ativas que ainda não podem cobrar pelo Stripe.</param>
/// <param name="Funerarias">Os mesmos números por funerária.</param>
/// <param name="GeradoEm">Quando os números foram calculados.</param>
public sealed record DashboardPlataformaResponse(
    IReadOnlyList<FunerariasNoStatus> FunerariasPorStatus,
    long ContratosAtivos,
    decimal VolumePagoNoMes,
    decimal ReceitaPlataformaNoMes,
    long FunerariasComPendenciaStripe,
    IReadOnlyList<IndicadoresDaFuneraria> Funerarias,
    DateTime GeradoEm);

public sealed record FunerariasNoStatus(string Status, long Quantidade);

public sealed class IndicadoresDaFuneraria
{
    public Guid IdTenant { get; init; }

    public string NomeFantasia { get; init; } = null!;

    public string Status { get; init; } = null!;

    public long ContratosAtivos { get; init; }

    public decimal VolumePagoNoMes { get; init; }

    public decimal VolumeTaxavelNoMes { get; init; }

    public bool PendenciaStripe { get; init; }
}

/// <summary>Consultas por período, com a mesma validação (REL-02). Sem <c>de</c> e <c>ate</c>, vale o mês atual.</summary>
public interface IConsultaPorPeriodo
{
    DateOnly? De { get; }

    DateOnly? Ate { get; }

    string? Formato { get; }
}

public sealed record RelatorioFaturasQuery(DateOnly? De, DateOnly? Ate, string? Status, string? Formato)
    : IQuery<IReadOnlyList<LinhaFatura>>, IConsultaPorPeriodo;

public sealed record RelatorioContratosQuery(DateOnly? De, DateOnly? Ate, string? Status, string? Formato)
    : IQuery<IReadOnlyList<LinhaContrato>>, IConsultaPorPeriodo;

public sealed record RelatorioFuneraisQuery(DateOnly? De, DateOnly? Ate, string? Status, string? Formato)
    : IQuery<IReadOnlyList<LinhaFuneral>>, IConsultaPorPeriodo;

public sealed class LinhaFatura
{
    public Guid IdFatura { get; init; }

    public Guid IdContrato { get; init; }

    public DateTime Competencia { get; init; }

    public decimal Valor { get; init; }

    public decimal ValorMultaJuros { get; init; }

    public DateTime DataVencimento { get; init; }

    public string Status { get; init; } = null!;

    public string? FormaPagamento { get; init; }

    public DateTime? DataPagamento { get; init; }

    public static readonly ColunaCsv<LinhaFatura>[] Colunas =
    [
        new("Fatura", l => l.IdFatura.ToString()),
        new("Contrato", l => l.IdContrato.ToString()),
        new("Competência", l => CsvRelatorio.Data(l.Competencia)),
        new("Valor", l => CsvRelatorio.Valor(l.Valor)),
        new("Multa e juros", l => CsvRelatorio.Valor(l.ValorMultaJuros)),
        new("Vencimento", l => CsvRelatorio.Data(l.DataVencimento)),
        new("Status", l => l.Status),
        new("Forma de pagamento", l => l.FormaPagamento),
        new("Pagamento", l => CsvRelatorio.Data(l.DataPagamento))
    ];
}

public sealed class LinhaContrato
{
    public Guid IdContrato { get; init; }

    public string NomePlano { get; init; } = null!;

    public string Periodicidade { get; init; } = null!;

    public decimal ValorTotal { get; init; }

    public string Status { get; init; } = null!;

    public DateTime DataContratacao { get; init; }

    public DateTime? DataFim { get; init; }

    public static readonly ColunaCsv<LinhaContrato>[] Colunas =
    [
        new("Contrato", l => l.IdContrato.ToString()),
        new("Plano", l => l.NomePlano),
        new("Periodicidade", l => l.Periodicidade),
        new("Valor", l => CsvRelatorio.Valor(l.ValorTotal)),
        new("Status", l => l.Status),
        new("Contratação", l => CsvRelatorio.Data(l.DataContratacao)),
        new("Fim", l => CsvRelatorio.Data(l.DataFim))
    ];
}

public sealed class LinhaFuneral
{
    public Guid IdFuneral { get; init; }

    public string NomeFalecido { get; init; } = null!;

    public string Status { get; init; } = null!;

    public string Destinacao { get; init; } = null!;

    public DateTime DataHoraPrevista { get; init; }

    public static readonly ColunaCsv<LinhaFuneral>[] Colunas =
    [
        new("Funeral", l => l.IdFuneral.ToString()),
        new("Falecido", l => l.NomeFalecido),
        new("Status", l => l.Status),
        new("Destinação", l => l.Destinacao),
        new("Data prevista", l => CsvRelatorio.Data(l.DataHoraPrevista))
    ];
}

public sealed record ColunaCsv<T>(string Titulo, Func<T, string?> Valor);

/// <summary>CSV para abrir direto no Excel em português: UTF-8 com BOM, ponto e vírgula, vírgula decimal.</summary>
public static class CsvRelatorio
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static bool Pedido(string? formato) => string.Equals(formato, "csv", StringComparison.OrdinalIgnoreCase);

    public static byte[] Gerar<T>(IEnumerable<ColunaCsv<T>> colunas, IEnumerable<T> linhas)
    {
        ColunaCsv<T>[] todas = [.. colunas];
        var csv = new StringBuilder();
        csv.AppendJoin(';', todas.Select(c => Celula(c.Titulo))).Append("\r\n");
        foreach (T linha in linhas)
        {
            csv.AppendJoin(';', todas.Select(c => Celula(c.Valor(linha)))).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    public static string Valor(decimal valor) => valor.ToString("0.00", PtBr);

    public static string Data(DateTime? data) => data?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Texto que começa como fórmula vira texto: o Excel executaria um nome como <c>=HYPERLINK(...)</c>.</summary>
    private static string Celula(string? valor)
    {
        string texto = valor ?? string.Empty;
        if (texto.Length > 0 && "=+-@\t\r".Contains(texto[0], StringComparison.Ordinal))
        {
            texto = "'" + texto;
        }

        return texto.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{texto.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : texto;
    }
}

internal abstract class ConsultaPorPeriodoValidator<T> : AbstractValidator<T>
    where T : IConsultaPorPeriodo
{
    private const int MaximoDias = 366;

    protected ConsultaPorPeriodoValidator()
    {
        RuleFor(c => c.Ate)
            .Must((c, ate) => c.De is null || ate is null || c.De <= ate).WithMessage("A data final não pode ser anterior à inicial.")
            .Must((c, ate) => c.De is null || ate is null || ate.Value.DayNumber - c.De.Value.DayNumber <= MaximoDias)
            .WithMessage($"O período pode ter no máximo {MaximoDias} dias.");
        RuleFor(c => c.De).NotNull().When(c => c.Ate is not null).WithMessage("Informe também a data inicial.");
        RuleFor(c => c.Ate).NotNull().When(c => c.De is not null).WithMessage("Informe também a data final.");
        RuleFor(c => c.Formato)
            .Must(f => f is null || CsvRelatorio.Pedido(f) || string.Equals(f, "json", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Formato inválido. Use json ou csv.");
    }
}

internal sealed class RelatorioFaturasQueryValidator : ConsultaPorPeriodoValidator<RelatorioFaturasQuery>;

internal sealed class RelatorioContratosQueryValidator : ConsultaPorPeriodoValidator<RelatorioContratosQuery>;

internal sealed class RelatorioFuneraisQueryValidator : ConsultaPorPeriodoValidator<RelatorioFuneraisQuery>;

/// <summary>Início e fim do período, no calendário da funerária, para colunas de data e de data e hora.</summary>
internal sealed record Periodo(DateTime Inicio, DateTime Fim, DateTime InicioUtc, DateTime FimUtc)
{
    public static Periodo DoMes(TimeProvider timeProvider)
    {
        DateTimeOffset agora = timeProvider.GetLocalNow();
        var inicio = new DateOnly(agora.Year, agora.Month, 1);
        return De(inicio, inicio.AddMonths(1).AddDays(-1), agora.Offset);
    }

    public static Periodo De(DateOnly? de, DateOnly? ate, TimeProvider timeProvider) =>
        de is DateOnly inicio && ate is DateOnly fim ? De(inicio, fim, timeProvider.GetLocalNow().Offset) : DoMes(timeProvider);

    private static Periodo De(DateOnly inicio, DateOnly ultimoDia, TimeSpan fuso)
    {
        DateTime comeca = inicio.ToDateTime(TimeOnly.MinValue);
        DateTime termina = ultimoDia.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return new Periodo(
            comeca, termina, new DateTimeOffset(comeca, fuso).UtcDateTime, new DateTimeOffset(termina, fuso).UtcDateTime);
    }
}

public sealed class DashboardQueryHandler(
    IDbConnectionFactory conexoes, ITenantContext tenantContext, ICacheDeRelatorios cache, TimeProvider timeProvider)
    : IQueryHandler<DashboardQuery, DashboardResponse>
{
    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(1);

    private const string Sql = """
        SELECT
            (SELECT count(*) FROM relatorios.vw_contratos
             WHERE id_tenant = @Tenant AND status = 'Ativo') AS "ContratosAtivos",
            (SELECT count(*) FROM relatorios.vw_contratos
             WHERE id_tenant = @Tenant AND data_contratacao >= @Inicio AND data_contratacao < @Fim) AS "NovosContratosNoMes",
            (SELECT count(*) FROM relatorios.vw_contratos
             WHERE id_tenant = @Tenant AND status IN ('Ativo', 'Suspenso')) AS "ContratosEmVigor",
            (SELECT count(DISTINCT c.id_contrato)
             FROM relatorios.vw_contratos c
             JOIN relatorios.vw_faturas f ON f.id_contrato = c.id_contrato AND f.id_tenant = c.id_tenant
             WHERE c.id_tenant = @Tenant AND c.status IN ('Ativo', 'Suspenso')
               AND f.status = 'Vencida' AND NOT f.fora_da_inadimplencia) AS "ContratosComVencida",
            (SELECT coalesce(sum(valor), 0) FROM relatorios.vw_faturas
             WHERE id_tenant = @Tenant AND status <> 'Cancelada' AND data_vencimento >= @Inicio AND data_vencimento < @Fim) AS "ReceitaPrevistaNoMes",
            (SELECT coalesce(sum(valor), 0) FROM relatorios.vw_faturas
             WHERE id_tenant = @Tenant AND status = 'Paga' AND data_pagamento >= @InicioUtc AND data_pagamento < @FimUtc) AS "ReceitaRecebidaNoMes",
            (SELECT count(*) FROM relatorios.vw_funerais
             WHERE id_tenant = @Tenant AND status NOT IN ('Concluido', 'Cancelado')) AS "FuneraisEmAndamento",
            (SELECT count(*) FROM relatorios.vw_translados
             WHERE id_tenant = @Tenant AND status NOT IN ('Entregue', 'Cancelado')) AS "TransladosEmAndamento"
        """;

    public async ValueTask<Result<DashboardResponse>> Handle(DashboardQuery query, CancellationToken cancellationToken)
    {
        Guid tenant = tenantContext.RequiredTenantId;
        return await cache.ObterAsync($"dashboard:{tenant}", Validade, () => CalcularAsync(tenant, cancellationToken));
    }

    private async Task<DashboardResponse> CalcularAsync(Guid tenant, CancellationToken cancellationToken)
    {
        Periodo mes = Periodo.DoMes(timeProvider);
        await using DbConnection conexao = await conexoes.OpenConnectionAsync(cancellationToken);
        Indicadores i = await conexao.QuerySingleAsync<Indicadores>(new CommandDefinition(
            Sql, new { Tenant = tenant, mes.Inicio, mes.Fim, mes.InicioUtc, mes.FimUtc }, cancellationToken: cancellationToken));

        decimal taxa = i.ContratosEmVigor == 0 ? 0 : decimal.Round(i.ContratosComVencida * 100m / i.ContratosEmVigor, 2);
        return new DashboardResponse(
            i.ContratosAtivos, i.NovosContratosNoMes, taxa, i.ReceitaPrevistaNoMes, i.ReceitaRecebidaNoMes,
            i.FuneraisEmAndamento, i.TransladosEmAndamento, timeProvider.GetUtcNow().UtcDateTime);
    }

    private sealed class Indicadores
    {
        public long ContratosAtivos { get; init; }

        public long NovosContratosNoMes { get; init; }

        public long ContratosEmVigor { get; init; }

        public long ContratosComVencida { get; init; }

        public decimal ReceitaPrevistaNoMes { get; init; }

        public decimal ReceitaRecebidaNoMes { get; init; }

        public long FuneraisEmAndamento { get; init; }

        public long TransladosEmAndamento { get; init; }
    }
}

public sealed class DashboardPlataformaQueryHandler(
    IDbConnectionFactory conexoes, IParametrosPlataforma parametros, IAuditoria auditoria, TimeProvider timeProvider)
    : IQueryHandler<DashboardPlataformaQuery, DashboardPlataformaResponse>
{
    private const string Sql = """
        SELECT
            t.id_tenant AS "IdTenant",
            t.nome_fantasia AS "NomeFantasia",
            t.status AS "Status",
            (SELECT count(*) FROM relatorios.vw_contratos c WHERE c.id_tenant = t.id_tenant AND c.status = 'Ativo') AS "ContratosAtivos",
            (SELECT coalesce(sum(f.valor), 0) FROM relatorios.vw_faturas f
             WHERE f.id_tenant = t.id_tenant AND f.status = 'Paga' AND f.data_pagamento >= @InicioUtc AND f.data_pagamento < @FimUtc) AS "VolumePagoNoMes",
            (SELECT coalesce(sum(f.valor), 0) FROM relatorios.vw_faturas f
             WHERE f.id_tenant = t.id_tenant AND f.status = 'Paga' AND f.forma_pagamento <> 'Manual'
               AND f.data_pagamento >= @InicioUtc AND f.data_pagamento < @FimUtc) AS "VolumeTaxavelNoMes",
            t.status = 'Ativa' AND NOT EXISTS (
                SELECT 1 FROM relatorios.vw_contas_pagamento p WHERE p.id_tenant = t.id_tenant AND p.cobrancas_habilitadas) AS "PendenciaStripe"
        FROM relatorios.vw_funerarias t
        ORDER BY t.nome_fantasia
        """;

    public async ValueTask<Result<DashboardPlataformaResponse>> Handle(DashboardPlataformaQuery query, CancellationToken cancellationToken)
    {
        Periodo mes = Periodo.DoMes(timeProvider);
        IReadOnlyList<IndicadoresDaFuneraria> funerarias;
        await using (DbConnection conexao = await conexoes.OpenConnectionAsync(cancellationToken))
        {
            funerarias = [.. await conexao.QueryAsync<IndicadoresDaFuneraria>(new CommandDefinition(
                Sql, new { mes.InicioUtc, mes.FimUtc }, cancellationToken: cancellationToken))];
        }

        // Todo acesso do ADMIN a dados de funerárias fica registrado.
        await auditoria.RegistrarAsync(new RegistroAuditoria(AcoesAuditadas.DashboardPlataformaConsultado, "Plataforma", null), cancellationToken);

        return new DashboardPlataformaResponse(
            [.. funerarias.GroupBy(f => f.Status).OrderBy(g => g.Key).Select(g => new FunerariasNoStatus(g.Key, g.Count()))],
            funerarias.Sum(f => f.ContratosAtivos),
            funerarias.Sum(f => f.VolumePagoNoMes),
            decimal.Round(funerarias.Sum(f => f.VolumeTaxavelNoMes) * parametros.TaxaPlataformaPercentual / 100m, 2),
            funerarias.Count(f => f.PendenciaStripe),
            funerarias,
            timeProvider.GetUtcNow().UtcDateTime);
    }
}

public sealed class RelatorioFaturasQueryHandler(RelatoriosDaFuneraria relatorios)
    : IQueryHandler<RelatorioFaturasQuery, IReadOnlyList<LinhaFatura>>
{
    private const string Sql = """
        SELECT id_fatura AS "IdFatura", id_contrato AS "IdContrato", competencia AS "Competencia", valor AS "Valor",
               valor_multa_juros AS "ValorMultaJuros", data_vencimento AS "DataVencimento", status AS "Status",
               forma_pagamento AS "FormaPagamento", data_pagamento AS "DataPagamento"
        FROM relatorios.vw_faturas
        WHERE id_tenant = @Tenant AND data_vencimento >= @Inicio AND data_vencimento < @Fim
          AND (@Status IS NULL OR upper(status) = @Status)
        ORDER BY data_vencimento, id_fatura
        """;

    public async ValueTask<Result<IReadOnlyList<LinhaFatura>>> Handle(RelatorioFaturasQuery query, CancellationToken cancellationToken) =>
        Result.Success(await relatorios.LerAsync<LinhaFatura>(Sql, "faturas", query, query.Status, cancellationToken));
}

public sealed class RelatorioContratosQueryHandler(RelatoriosDaFuneraria relatorios)
    : IQueryHandler<RelatorioContratosQuery, IReadOnlyList<LinhaContrato>>
{
    private const string Sql = """
        SELECT id_contrato AS "IdContrato", nome_plano AS "NomePlano", periodicidade AS "Periodicidade", valor_total AS "ValorTotal",
               status AS "Status", data_contratacao AS "DataContratacao", data_fim AS "DataFim"
        FROM relatorios.vw_contratos
        WHERE id_tenant = @Tenant AND data_contratacao >= @Inicio AND data_contratacao < @Fim
          AND (@Status IS NULL OR upper(status) = @Status)
        ORDER BY data_contratacao, id_contrato
        """;

    public async ValueTask<Result<IReadOnlyList<LinhaContrato>>> Handle(RelatorioContratosQuery query, CancellationToken cancellationToken) =>
        Result.Success(await relatorios.LerAsync<LinhaContrato>(Sql, "contratos", query, query.Status, cancellationToken));
}

public sealed class RelatorioFuneraisQueryHandler(RelatoriosDaFuneraria relatorios)
    : IQueryHandler<RelatorioFuneraisQuery, IReadOnlyList<LinhaFuneral>>
{
    private const string Sql = """
        SELECT id_funeral AS "IdFuneral", nome_falecido AS "NomeFalecido", status AS "Status", destinacao AS "Destinacao",
               data_hora_prevista AS "DataHoraPrevista"
        FROM relatorios.vw_funerais
        WHERE id_tenant = @Tenant AND data_hora_prevista >= @InicioUtc AND data_hora_prevista < @FimUtc
          AND (@Status IS NULL OR upper(status) = @Status)
        ORDER BY data_hora_prevista, id_funeral
        """;

    public async ValueTask<Result<IReadOnlyList<LinhaFuneral>>> Handle(RelatorioFuneraisQuery query, CancellationToken cancellationToken) =>
        Result.Success(await relatorios.LerAsync<LinhaFuneral>(Sql, "funerais", query, query.Status, cancellationToken));
}

/// <summary>Leitura dos relatórios exportáveis (REL-02), sempre na funerária da requisição. A exportação fica registrada.</summary>
public sealed class RelatoriosDaFuneraria(
    IDbConnectionFactory conexoes, ITenantContext tenantContext, IAuditoria auditoria, TimeProvider timeProvider)
{
    internal async Task<IReadOnlyList<T>> LerAsync<T>(
        string sql, string relatorio, IConsultaPorPeriodo consulta, string? status, CancellationToken cancellationToken)
    {
        Periodo periodo = Periodo.De(consulta.De, consulta.Ate, timeProvider);

        // O status vem como na API (EM_PREPARACAO) e está gravado como no código (EmPreparacao).
        string? statusNormalizado = string.IsNullOrWhiteSpace(status) ? null : status.Replace("_", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();

        IReadOnlyList<T> linhas;
        await using (DbConnection conexao = await conexoes.OpenConnectionAsync(cancellationToken))
        {
            linhas = [.. await conexao.QueryAsync<T>(new CommandDefinition(
                sql,
                new { Tenant = tenantContext.RequiredTenantId, periodo.Inicio, periodo.Fim, periodo.InicioUtc, periodo.FimUtc, Status = statusNormalizado },
                cancellationToken: cancellationToken))];
        }

        if (CsvRelatorio.Pedido(consulta.Formato))
        {
            await auditoria.RegistrarAsync(new RegistroAuditoria(
                AcoesAuditadas.RelatorioExportado, "Relatorio", null,
                ValoresNovos: new { relatorio, de = periodo.Inicio, ate = periodo.Fim, linhas = linhas.Count }),
                cancellationToken);
        }

        return linhas;
    }
}
