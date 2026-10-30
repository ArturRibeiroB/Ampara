using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Catalogo.Application.Categorias;
using Funeraria.Modules.Catalogo.Application.Itens;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Catalogo.Endpoints;

/// <summary>
/// Catálogo de produtos e serviços (FUN-01). A consulta é pública na funerária do endereço, como os planos; a gestão
/// é do gestor (FUNERARIA). Nada é excluído: categorias, itens e variações são desativados.
/// </summary>
internal sealed class CatalogoEndpoints : IEndpoint
{
    private const string Tag = "Catálogo";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        MapPublico(app);
        MapGestao(app);
    }

    private static void MapPublico(IEndpointRouteBuilder app)
    {
        app.MapGet("catalogo", async (ISender sender, CancellationToken ct, Guid? categoria = null) =>
                (await sender.Send(new ListarItensQuery(categoria, SomenteAtivos: true), ct)).Match(Results.Ok))
            .AllowAnonymous()
            .RequireTenant()
            .WithTags(Tag)
            .WithName("ListarCatalogo")
            .WithSummary("Itens ativos do catálogo da funerária, por categoria (id)")
            .Produces<IReadOnlyList<ItemResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("catalogo/categorias", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListarCategoriasQuery(SomenteAtivas: true), ct)).Match(Results.Ok))
            .AllowAnonymous()
            .RequireTenant()
            .WithTags(Tag)
            .WithName("ListarCategoriasCatalogo")
            .WithSummary("Categorias ativas do catálogo, na ordem definida pela funerária")
            .Produces<IReadOnlyList<CategoriaResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("catalogo/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterItemQuery(id, SomenteAtivo: true), ct)).Match(Results.Ok))
            .AllowAnonymous()
            .RequireTenant()
            .WithTags(Tag)
            .WithName("ObterItemCatalogo")
            .WithSummary("Detalhe de um item ativo do catálogo")
            .Produces<ItemResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("funerarias/{slug}/catalogo/{id:guid}/imagem", async (string slug, Guid id, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                Result<ArquivoLido> imagem = await sender.Send(new ObterImagemItemQuery(slug, id), ct);
                if (imagem.IsFailure)
                {
                    return ApiResults.Problem(imagem);
                }

                // O endereço muda a cada upload (?v=), então dá para guardar em cache.
                http.Response.Headers.CacheControl = "public, max-age=86400";
                return Results.Stream(imagem.Value.Conteudo, imagem.Value.ContentType);
            })
            .AllowAnonymous()
            .WithTags(Tag)
            .WithName("ImagemItemCatalogo")
            .WithSummary("Imagem de um item do catálogo, para telas e e-mails")
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapGestao(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder gestao = app.MapGroup("gestao/catalogo")
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        gestao.MapGet(string.Empty, async (ISender sender, CancellationToken ct, Guid? categoria = null) =>
                (await sender.Send(new ListarItensQuery(categoria, SomenteAtivos: false), ct)).Match(Results.Ok))
            .WithName("ListarCatalogoGestao")
            .WithSummary("Todos os itens do catálogo, ativos e inativos, com estoque")
            .Produces<IReadOnlyList<ItemResponse>>();

        gestao.MapGet("categorias", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListarCategoriasQuery(SomenteAtivas: false), ct)).Match(Results.Ok))
            .WithName("ListarCategoriasGestao")
            .WithSummary("Todas as categorias, ativas e inativas")
            .Produces<IReadOnlyList<CategoriaResponse>>();

        gestao.MapPost("categorias", async (CategoriaRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CriarCategoriaCommand(r.Nome ?? string.Empty, r.Ordem), ct))
                    .Match(c => Results.Created($"/api/v1/gestao/catalogo/categorias/{c.IdCategoria}", c)))
            .WithName("CriarCategoria")
            .WithSummary("Cria uma categoria")
            .Produces<CategoriaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        gestao.MapPut("categorias/{id:guid}", async (Guid id, CategoriaRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AtualizarCategoriaCommand(id, r.Nome ?? string.Empty, r.Ordem), ct)).Match(Results.Ok))
            .WithName("AtualizarCategoria")
            .WithSummary("Altera nome e ordem da categoria")
            .Produces<CategoriaResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        gestao.MapPatch("categorias/{id:guid}/status", async (Guid id, StatusRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusCategoriaCommand(id, r.Ativo), ct)).Match(Results.NoContent))
            .WithName("AlterarStatusCategoria")
            .WithSummary("Ativa ou desativa a categoria; os itens dela somem do catálogo do cliente")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        gestao.MapPost(string.Empty, async (ItemRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CriarItemCommand(
                    r.IdCategoria, r.Nome ?? string.Empty, r.Descricao, r.Preco, r.ControlaEstoque, r.QuantidadeEstoque, r.IdTerceiroFornecedor), ct))
                    .Match(i => Results.Created($"/api/v1/catalogo/{i.IdItem}", i)))
            .WithName("CriarItemCatalogo")
            .WithSummary("Cria um item do catálogo")
            .Produces<ItemResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        gestao.MapPut("{id:guid}", async (Guid id, ItemRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AtualizarItemCommand(
                    id, r.IdCategoria, r.Nome ?? string.Empty, r.Descricao, r.Preco, r.ControlaEstoque, r.QuantidadeEstoque, r.IdTerceiroFornecedor), ct))
                    .Match(Results.Ok))
            .WithName("AtualizarItemCatalogo")
            .WithSummary("Altera o item. O preço novo não muda funerais e orçamentos já aprovados")
            .Produces<ItemResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        gestao.MapPatch("{id:guid}/status", async (Guid id, StatusRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusItemCommand(id, r.Ativo), ct)).Match(Results.NoContent))
            .WithName("AlterarStatusItemCatalogo")
            .WithSummary("Ativa ou desativa o item")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        gestao.MapPost("{id:guid}/imagem", async (Guid id, IFormFile? arquivo, ISender sender, CancellationToken ct) =>
            {
                await using Stream? conteudo = arquivo?.OpenReadStream();
                ArquivoEnviado? enviado = conteudo is null ? null : new ArquivoEnviado(arquivo!.FileName, conteudo);
                return (await sender.Send(new EnviarImagemItemCommand(id, enviado), ct)).Match(Results.Ok);
            })
            // API com token no cabeçalho, sem cookie de sessão: não há CSRF a evitar aqui.
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithName("EnviarImagemItemCatalogo")
            .WithSummary("Envia a imagem do item (campo \"arquivo\"): PNG, JPEG ou WEBP de até 5 MB")
            .Produces<ItemResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        gestao.MapPost("{id:guid}/variacoes", async (Guid id, VariacaoRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AdicionarVariacaoCommand(id, r.Nome ?? string.Empty, r.AcrescimoPreco), ct)).Match(Results.Ok))
            .WithName("AdicionarVariacao")
            .WithSummary("Adiciona uma variação ao item (ex.: urna em mogno)")
            .Produces<ItemResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        gestao.MapPatch("{id:guid}/variacoes/{idVariacao:guid}/status", async (Guid id, Guid idVariacao, StatusRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusVariacaoCommand(id, idVariacao, r.Ativo), ct)).Match(Results.NoContent))
            .WithName("AlterarStatusVariacao")
            .WithSummary("Ativa ou desativa uma variação")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record CategoriaRequest(string? Nome, int? Ordem);

public sealed record ItemRequest(
    Guid? IdCategoria,
    string? Nome,
    string? Descricao,
    decimal? Preco,
    bool ControlaEstoque,
    int? QuantidadeEstoque,
    Guid? IdTerceiroFornecedor = null);

public sealed record VariacaoRequest(string? Nome, decimal? AcrescimoPreco);

public sealed record StatusRequest(bool? Ativo);
