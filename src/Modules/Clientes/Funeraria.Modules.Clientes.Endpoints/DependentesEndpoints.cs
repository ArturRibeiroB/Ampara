using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Clientes.Application;
using Funeraria.Modules.Clientes.Application.Commands.Dependentes;
using Funeraria.Modules.Clientes.Application.Queries.Dependentes;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Clientes.Endpoints;

/// <summary>Dependentes do titular logado (card Criar Dependente, CLI-02). O titular vem sempre do token.</summary>
internal sealed class DependentesEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("dependentes")
            .RequireAuthorization(Politicas.Cliente)
            .WithTags("Dependentes")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost(string.Empty, async (CadastrarDependenteRequest r, ISender sender, CancellationToken ct) =>
            {
                var command = new CadastrarDependenteCommand(
                    r.Nome ?? string.Empty,
                    r.Cpf ?? string.Empty,
                    Datas.Ler(r.DataNascimento),
                    r.Parentesco ?? string.Empty,
                    r.Telefone,
                    r.Email);

                return (await sender.Send(command, ct))
                    .Match(d => Results.Created($"/api/v1/dependentes/{d.IdDependente}", d));
            })
            .WithName("CadastrarDependente")
            .WithSummary("Cadastra um dependente do titular logado")
            .Produces<DependenteResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet(string.Empty, async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListarDependentesQuery(), ct)).Match(Results.Ok))
            .WithName("ListarDependentes")
            .WithSummary("Lista os dependentes do titular logado")
            .Produces<IReadOnlyList<DependenteResponse>>();

        group.MapGet("{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterDependenteQuery(id), ct)).Match(Results.Ok))
            .WithName("ObterDependente")
            .WithSummary("Detalhe de um dependente do titular logado")
            .Produces<DependenteResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("{id:guid}", async (Guid id, AtualizarDependenteRequest r, ISender sender, CancellationToken ct) =>
            {
                var command = new AtualizarDependenteCommand(
                    id,
                    r.Nome ?? string.Empty,
                    Datas.Ler(r.DataNascimento),
                    r.Parentesco ?? string.Empty,
                    r.Telefone,
                    r.Email);

                return (await sender.Send(command, ct)).Match(Results.Ok);
            })
            .WithName("AtualizarDependente")
            .WithSummary("Edita um dependente do titular logado. O CPF não muda")
            .Produces<DependenteResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RemoverDependenteCommand(id), ct)).Match(Results.NoContent))
            .WithName("RemoverDependente")
            .WithSummary("Remove um dependente (remoção lógica). Não é permitido com contrato ativo")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
