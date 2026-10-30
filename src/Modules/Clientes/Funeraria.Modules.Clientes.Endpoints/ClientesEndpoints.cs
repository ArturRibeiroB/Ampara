using Funeraria.Modules.Clientes.Application.Queries.ExportarMeusDados;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Clientes.Application;
using Funeraria.Modules.Clientes.Application.Commands.AtualizarMeusDados;
using Funeraria.Modules.Clientes.Application.Commands.CadastrarCliente;
using Funeraria.Modules.Clientes.Application.Queries.Gestao;
using Funeraria.Modules.Clientes.Application.Queries.ObterMeusDados;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Clientes.Endpoints;

/// <summary>Cadastro do cliente (card Criar Cliente), meus dados (CLI-01) e busca pela equipe (CLI-03).</summary>
internal sealed class ClientesEndpoints : IEndpoint
{
    private const string Tag = "Clientes";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("clientes", async (CadastrarClienteRequest r, ISender sender, CancellationToken ct) =>
            {
                var command = new CadastrarClienteCommand(
                    r.Nome ?? string.Empty,
                    r.Cpf ?? string.Empty,
                    Datas.Ler(r.DataNascimento),
                    r.Email ?? string.Empty,
                    r.Senha ?? string.Empty,
                    r.Telefone,
                    r.Endereco,
                    r.Cep,
                    r.Cidade,
                    r.Estado,
                    r.AceiteTermos ?? false);

                return (await sender.Send(command, ct)).Match(c => Results.Created("/api/v1/clientes/me", c));
            })
            .AllowAnonymous()
            .RequireTenant()
            .RequireRateLimiting(PoliticasLimite.CadastroCliente)
            .WithTags(Tag)
            .WithName("CadastrarCliente")
            .WithSummary("Cadastra o cliente titular na funerária do endereço acessado")
            .WithDescription(
                "Cria a conta com a senha informada. Se o e-mail já tiver conta na plataforma (cliente de outra funerária), " +
                "a senha precisa ser a dessa conta e só o vínculo com esta funerária é criado.")
            .Produces<ClienteResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        RouteGroupBuilder me = app.MapGroup("clientes/me")
            .RequireAuthorization(Politicas.Cliente)
            .WithTags(Tag)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        me.MapGet("exportacao", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ExportarMeusDadosQuery(), ct)).Match(Results.Ok))
            .WithName("ExportarMeusDados")
            .WithSummary("Todos os dados do titular nesta funerária, para download (LGPD)")
            .WithDescription("Cadastro, dependentes, contratos e histórico de aceite dos termos (SEG-01).")
            .Produces<ExportacaoDadosResponse>();

        me.MapGet(string.Empty, async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterMeusDadosQuery(), ct)).Match(Results.Ok))
            .WithName("ObterMeusDados")
            .WithSummary("Dados do cliente logado")
            .Produces<ClienteResponse>();

        me.MapPut(string.Empty, async (AtualizarMeusDadosRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AtualizarMeusDadosCommand(r.Nome ?? string.Empty, r.Telefone, r.Endereco, r.Cep, r.Cidade, r.Estado), ct))
                    .Match(Results.Ok))
            .WithName("AtualizarMeusDados")
            .WithSummary("Atualiza os dados do cliente logado. CPF, data de nascimento e e-mail não mudam aqui")
            .Produces<ClienteResponse>()
            .ProducesValidationProblem();

        RouteGroupBuilder gestao = app.MapGroup("gestao/clientes")
            .RequireAuthorization(Politicas.Atendimento)
            .WithTags(Tag)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        gestao.MapGet(string.Empty, async (
                ISender sender,
                CancellationToken ct,
                string? nome = null,
                string? cpf = null,
                [FromQuery(Name = "status_contrato")] string? statusContrato = null,
                int pagina = 1,
                int tamanho = PaginaRequest.TamanhoPadrao,
                string? ordenarPor = null,
                string? direcao = null) =>
                (await sender.Send(new BuscarClientesQuery(
                    new PaginaRequest(pagina, tamanho), nome, cpf, new OrdenacaoRequest(ordenarPor, direcao), statusContrato), ct)).Match(Results.Ok))
            .WithName("BuscarClientes")
            .WithSummary("Busca clientes da funerária por nome, CPF ou status do contrato")
            .WithDescription("status_contrato = ATIVO, A_PAGAR, SUSPENSO, CANCELADO (tem pelo menos um contrato no status) ou SEM_CONTRATO. Ordenação: ordenarPor = nome (padrão), cidade ou cadastrado_em; direcao = asc ou desc. O CPF da listagem vem mascarado.")
            .ProducesValidationProblem()
            .Produces<PaginaResult<ClienteResumoResponse>>();

        gestao.MapGet("{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterClienteGestaoQuery(id), ct)).Match(Results.Ok))
            .WithName("ObterClienteGestao")
            .WithSummary("Detalhe do cliente com os dependentes")
            .Produces<ClienteDetalheResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
