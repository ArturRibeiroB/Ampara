using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Identidade.Application.Equipe;
using Funeraria.Modules.Identidade.Domain.Equipe;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Identidade.Endpoints.Equipe;

/// <summary>Equipe da funerária: cadastro de funcionários e consultores e gestão dos acessos (AUT-07, AUT-08, DEC-11).</summary>
internal sealed class EquipeEndpoints : IEndpoint
{
    private const string Tag = "Equipe da funerária";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder gestao = app.MapGroup("gestao")
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        gestao.MapPost("funcionarios", async (CadastrarFuncionarioRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CadastrarFuncionarioCommand(
                        r.Nome ?? string.Empty, r.Cpf ?? string.Empty, r.Email ?? string.Empty, r.Cargo, r.Telefone, r.Perfil), ct))
                    .Match(m => Results.Created($"/api/v1/gestao/usuarios/{m.IdUsuario}", m)))
            .ComMensagemAcessoNegado(EquipeErros.SomenteGestorCadastra)
            .WithName("CadastrarFuncionario")
            .WithSummary("Cadastra um funcionário ou consultor e envia o convite por e-mail")
            .WithDescription("Perfil: FUNCIONARIO (padrão) ou CONSULTOR. Quem ainda não tem conta recebe o link para definir a senha; "
                + "quem já tem conta na plataforma recebe o aviso de acesso.")
            .Produces<MembroEquipeResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        gestao.MapGet("usuarios", async (
                ISender sender,
                CancellationToken ct,
                string? perfil = null,
                string? status = null,
                string? busca = null,
                int pagina = 1,
                int tamanho = PaginaRequest.TamanhoPadrao) =>
                (await sender.Send(new ListarEquipeQuery(new PaginaRequest(pagina, tamanho), perfil, status, busca), ct)).Match(Results.Ok))
            .WithName("ListarEquipe")
            .WithSummary("Equipe da funerária, por perfil, status (ATIVO ou INATIVO) e nome ou e-mail")
            .Produces<PaginaResult<MembroEquipeResponse>>()
            .ProducesValidationProblem();

        gestao.MapPatch("usuarios/{id:guid}/status", async (Guid id, AlterarStatusMembroRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarStatusMembroCommand(id, r.Ativo), ct)).Match(Results.NoContent))
            .WithName("AlterarStatusMembro")
            .WithSummary("Ativa ou desativa o acesso à funerária")
            .WithDescription("Desativar encerra as sessões desta funerária; a conta continua valendo nas outras. "
                + "O gestor não desativa o próprio acesso nem o último gestor ativo.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        gestao.MapPatch("usuarios/{id:guid}/perfil", async (Guid id, AlterarPerfilMembroRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarPerfilMembroCommand(id, r.Perfil), ct)).Match(Results.NoContent))
            .WithName("AlterarPerfilMembro")
            .WithSummary("Troca o perfil: FUNERARIA, FUNCIONARIO ou CONSULTOR")
            .WithDescription("Vale a partir da próxima renovação da sessão. O último gestor ativo não pode ser rebaixado.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

/// <summary>Corpo de POST /gestao/funcionarios. Campos opcionais para a validação devolver as mensagens.</summary>
public sealed record CadastrarFuncionarioRequest(
    string? Nome,
    string? Cpf,
    string? Email,
    string? Cargo,
    string? Telefone,
    PerfilVinculo? Perfil);

public sealed record AlterarStatusMembroRequest(bool? Ativo);

public sealed record AlterarPerfilMembroRequest(PerfilVinculo? Perfil);
