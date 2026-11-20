using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Relatorios.Application;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Relatorios.Endpoints;

/// <summary>Painel da funerária (REL-01), relatórios exportáveis (REL-02) e painel da plataforma (REL-03).</summary>
internal sealed class RelatoriosEndpoints : IEndpoint
{
    private const string Tag = "Relatorios";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("gestao/dashboard", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new DashboardQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("DashboardFuneraria")
            .WithSummary("Indicadores da funerária no mês atual")
            .WithDescription("Calculados com no máximo um minuto de atraso. A taxa de inadimplência é a parte dos contratos em vigor com fatura do plano vencida.")
            .Produces<DashboardResponse>();

        app.MapGet("admin/dashboard", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new DashboardPlataformaQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.Plataforma)
            .WithTags(Tag)
            .WithName("DashboardPlataforma")
            .WithSummary("Indicadores da plataforma, sem dados pessoais")
            .WithDescription("A receita é uma estimativa da taxa sobre os pagamentos do mês pelo Stripe (DEC-09). A consulta fica registrada na auditoria.")
            .Produces<DashboardPlataformaResponse>();

        RouteGroupBuilder relatorios = app.MapGroup("gestao/relatorios")
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag);

        relatorios.MapGet("financeiro", async (DateOnly? de, DateOnly? ate, string? status, string? formato, ISender sender, CancellationToken ct) =>
                Responder(await sender.Send(new RelatorioFaturasQuery(de, ate, status, formato), ct), formato, "faturas.csv", LinhaFatura.Colunas))
            .WithName("RelatorioFinanceiro")
            .WithSummary("Faturas por período de vencimento e status")
            .WithDescription(Filtros)
            .Produces<IReadOnlyList<LinhaFatura>>()
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesValidationProblem();

        relatorios.MapGet("contratos", async (DateOnly? de, DateOnly? ate, string? status, string? formato, ISender sender, CancellationToken ct) =>
                Responder(await sender.Send(new RelatorioContratosQuery(de, ate, status, formato), ct), formato, "contratos.csv", LinhaContrato.Colunas))
            .WithName("RelatorioContratos")
            .WithSummary("Contratos por período de contratação e status")
            .WithDescription(Filtros)
            .Produces<IReadOnlyList<LinhaContrato>>()
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesValidationProblem();

        relatorios.MapGet("funerais", async (DateOnly? de, DateOnly? ate, string? status, string? formato, ISender sender, CancellationToken ct) =>
                Responder(await sender.Send(new RelatorioFuneraisQuery(de, ate, status, formato), ct), formato, "funerais.csv", LinhaFuneral.Colunas))
            .WithName("RelatorioFunerais")
            .WithSummary("Funerais por período da cerimônia e status")
            .WithDescription(Filtros)
            .Produces<IReadOnlyList<LinhaFuneral>>()
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesValidationProblem();
    }

    private const string Filtros =
        "Sem de e ate, vale o mês atual; o período vai até 366 dias. Com formato=csv, devolve o arquivo (ponto e vírgula, UTF-8), e a exportação fica registrada na auditoria.";

    private static IResult Responder<T>(Result<IReadOnlyList<T>> resultado, string? formato, string arquivo, ColunaCsv<T>[] colunas) =>
        resultado.Match(linhas => CsvRelatorio.Pedido(formato)
            ? Results.File(CsvRelatorio.Gerar(colunas, linhas), "text/csv; charset=utf-8", arquivo)
            : Results.Ok(linhas));
}
