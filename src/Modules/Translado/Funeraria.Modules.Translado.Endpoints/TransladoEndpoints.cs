using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Documentos.Contracts;
using Funeraria.Modules.Translado.Application.Translados;
using Funeraria.Modules.Translado.Domain.Translados;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Translado.Endpoints;

/// <summary>
/// Translado (TRA-01 a TRA-05). A equipe interna registra, atribui e acompanha; o titular consulta; a empresa atribuída usa o
/// portal do terceiro, com a visão reduzida. O aceite e a recusa são pela atribuição (POST /terceiros/atribuicoes/{id}/aceite e /recusa).
/// </summary>
internal sealed class TransladoEndpoints : IEndpoint
{
    private const string Tag = "Translado";
    private const string TagPortal = "Portal do terceiro";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("funerais/{id:guid}/translados", async (Guid id, RegistrarTransladoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RegistrarTransladoCommand(id, r.Origem, r.Destino, r.DataPrevista, r.DistanciaKm), ct))
                .Match(t => Results.Created($"/api/v1/translados/{t.IdTranslado}", t)))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("RegistrarTranslado")
            .WithSummary("Registra o translado do funeral; o tipo e o valor são calculados aqui")
            .WithDescription("Sem distancia_km, a distância vem da API de rotas; se ela não encontrar o endereço, a resposta 400 pede os km. "
                + "Translado internacional ainda não está disponível.")
            .Produces<TransladoResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("funerais/{id:guid}/translados", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new TransladosDoFuneralQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("TransladosDoFuneral")
            .WithSummary("Translados do funeral")
            .Produces<IReadOnlyList<TransladoResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("translados/simular", async (SimularTransladoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SimularTransladoCommand(r.Origem, r.Destino, r.DistanciaKm, r.IdFuneral), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.Atendimento)
            .WithTags(Tag)
            .WithName("SimularTranslado")
            .WithSummary("Calcula o preço antes de registrar; com id_funeral, desconta os km do plano")
            .Produces<SimulacaoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("translados/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterTransladoQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("ObterTranslado")
            .WithSummary("Detalhe do translado (o titular vê os do próprio contrato)")
            .Produces<TransladoResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("translados/{id:guid}/historico", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new HistoricoTransladoQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("HistoricoTranslado")
            .WithSummary("Histórico de status do translado")
            .Produces<IReadOnlyList<HistoricoTransladoResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("translados/{id:guid}/documentos", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PendenciasDocumentosQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("PendenciasDocumentosTranslado")
            .WithSummary("Documentos exigidos pelo tipo do translado e o que falta anexar")
            .Produces<PendenciasDocumentosResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("translados/{id:guid}/empresas-disponiveis", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new EmpresasDisponiveisQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("EmpresasDisponiveisTranslado")
            .WithSummary("Empresas de frota ativas que atendem a origem do translado")
            .Produces<IReadOnlyList<EmpresaDisponivelResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPatch("translados/{id:guid}/terceiro", async (Guid id, AtribuirFrotaRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AtribuirFrotaCommand(id, r.IdTerceiro), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("AtribuirFrotaTranslado")
            .WithSummary("Atribui o translado a uma empresa de frota")
            .Produces<TransladoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPatch("translados/{id:guid}/status", async (Guid id, StatusTransladoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusTransladoCommand(id, r.Status, r.Observacao), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("AlterarStatusTranslado")
            .WithSummary("Muda o status: EM_COLETA, EM_TRANSITO, ENTREGUE ou CANCELADO")
            .WithDescription("A coleta exige empresa que aceitou e os documentos obrigatórios. Cada mudança entra no histórico e vai para o hub /hubs/translados.")
            .Produces<TransladoResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        MapConfiguracao(app);
        MapPortal(app);
    }

    private static void MapConfiguracao(IEndpointRouteBuilder app)
    {
        app.MapGet("gestao/translados/tabela-precos", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterConfiguracaoTransladoQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("TabelaPrecosTranslado")
            .WithSummary("Tabela de preços por faixa de km e documentos exigidos por tipo")
            .Produces<ConfiguracaoTransladoResponse>();

        app.MapPut("gestao/translados/tabela-precos", async (TabelaPrecosRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DefinirTabelaPrecosCommand(r.Faixas), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag)
            .WithName("DefinirTabelaPrecosTranslado")
            .WithSummary("Troca a tabela de preços")
            .WithDescription("Faixas contínuas a partir de 0 km, terminando numa faixa aberta (ate_km vazio) com valor_km_excedente. Translados já registrados não mudam.")
            .Produces<ConfiguracaoTransladoResponse>()
            .ProducesValidationProblem();

        app.MapPut("gestao/translados/documentos-exigidos", async (DocumentosExigidosRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DefinirDocumentosExigidosCommand(r.Documentos), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag)
            .WithName("DefinirDocumentosExigidosTranslado")
            .WithSummary("Documentos exigidos por tipo de translado (MUNICIPAL, INTERMUNICIPAL, INTERESTADUAL)")
            .Produces<ConfiguracaoTransladoResponse>()
            .ProducesValidationProblem();
    }

    private static void MapPortal(IEndpointRouteBuilder app)
    {
        app.MapGet("terceiros/translados", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new MeusTransladosQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.TerceiroFrota)
            .WithTags(TagPortal)
            .WithName("MeusTranslados")
            .WithSummary("Translados da empresa logada: origem, destino, horário e contato, sem valores nem contrato")
            .Produces<IReadOnlyList<TransladoFrotaResponse>>();

        app.MapGet("terceiros/translados/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterTransladoFrotaQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.TerceiroFrota)
            .WithTags(TagPortal)
            .WithName("ObterTransladoFrota")
            .WithSummary("Translado atribuído à empresa logada")
            .Produces<TransladoFrotaResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPut("terceiros/translados/{id:guid}/motorista", async (Guid id, MotoristaRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new InformarMotoristaCommand(id, r.Nome, r.Telefone, r.Placa, r.Modelo), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.TerceiroFrota)
            .WithTags(TagPortal)
            .WithName("InformarMotoristaTranslado")
            .WithSummary("Informa o motorista e o veículo, depois do aceite")
            .Produces<TransladoFrotaResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPatch("terceiros/translados/{id:guid}/status", async (Guid id, StatusTransladoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusPelaFrotaCommand(id, r.Status, r.Observacao), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.TerceiroFrota)
            .WithTags(TagPortal)
            .WithName("AlterarStatusTransladoFrota")
            .WithSummary("A empresa atualiza o status do translado atribuído a ela")
            .Produces<TransladoFrotaResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("terceiros/translados/{id:guid}/documentos/{idDocumento:guid}/download", async (Guid id, Guid idDocumento, ISender sender, CancellationToken ct) =>
                (await sender.Send(new BaixarDocumentoFrotaQuery(id, idDocumento), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.TerceiroFrota)
            .WithTags(TagPortal)
            .WithName("BaixarDocumentoTransladoFrota")
            .WithSummary("Link temporário de um documento do translado atribuído à empresa")
            .Produces<LinkDocumentoDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record RegistrarTransladoRequest(EnderecoRequest? Origem, EnderecoRequest? Destino, DateTime? DataPrevista, decimal? DistanciaKm);

public sealed record SimularTransladoRequest(EnderecoRequest? Origem, EnderecoRequest? Destino, decimal? DistanciaKm, Guid? IdFuneral);

public sealed record AtribuirFrotaRequest(Guid? IdTerceiro);

public sealed record StatusTransladoRequest(StatusTranslado? Status, string? Observacao);

public sealed record MotoristaRequest(string? Nome, string? Telefone, string? Placa, string? Modelo);

public sealed record TabelaPrecosRequest(IReadOnlyList<FaixaKm>? Faixas);

public sealed record DocumentosExigidosRequest(IReadOnlyDictionary<TipoTranslado, IReadOnlyList<string>>? Documentos);
