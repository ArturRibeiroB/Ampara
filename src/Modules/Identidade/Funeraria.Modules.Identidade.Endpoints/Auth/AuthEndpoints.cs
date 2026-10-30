using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Identidade.Application.Commands.AlterarSenha;
using Funeraria.Modules.Identidade.Application.Commands.ConfirmarEmail;
using Funeraria.Modules.Identidade.Application.Commands.DefinirSenha;
using Funeraria.Modules.Identidade.Application.Commands.EncerrarSessao;
using Funeraria.Modules.Identidade.Application.Commands.Login;
using Funeraria.Modules.Identidade.Application.Commands.RedefinirSenha;
using Funeraria.Modules.Identidade.Application.Commands.RenovarSessao;
using Funeraria.Modules.Identidade.Application.Commands.SolicitarRedefinicaoSenha;
using Funeraria.Modules.Identidade.Application.Queries.MinhasFunerarias;
using Funeraria.Modules.Identidade.Application.Sessoes;
using Funeraria.Modules.Identidade.Application.Termos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Identidade.Endpoints.Auth;

/// <summary>Login, renovação, logout, senha, confirmação de e-mail e funerárias do usuário (AUT-02, AUT-03, AUT-05).</summary>
internal sealed class AuthEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("auth").WithTags("Autenticação");

        group.MapPost("login", async (LoginRequest request, ISender sender, HttpContext http, CancellationToken ct) =>
                Sessao(await sender.Send(new LoginCommand(request.Email ?? string.Empty, request.Senha ?? string.Empty), ct), http))
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Autenticacao)
            .WithName("Login")
            .WithSummary("Entra na funerária do endereço acessado (ou na plataforma, para o ADMIN)")
            .WithDescription("Devolve o access token no corpo e o refresh token em cookie httpOnly.")
            .Produces<LoginResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("refresh", async (ISender sender, HttpContext http, CancellationToken ct) =>
                Sessao(await sender.Send(new RenovarSessaoCommand(RefreshTokenCookie.Ler(http)), ct), http))
            .AllowAnonymous()
            .WithName("RenovarSessao")
            .WithSummary("Troca o refresh token do cookie por um access token novo")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("logout", async (ISender sender, HttpContext http, CancellationToken ct) =>
            {
                Result result = await sender.Send(new EncerrarSessaoCommand(RefreshTokenCookie.Ler(http)), ct);
                RefreshTokenCookie.Remover(http);
                return result.Match(Results.NoContent);
            })
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Encerra a sessão e revoga o refresh token")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("definir-senha", async (DefinirSenhaRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DefinirSenhaCommand(request.Token ?? string.Empty, request.Senha ?? string.Empty), ct))
                    .Match(Results.NoContent))
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Autenticacao)
            .WithName("DefinirSenha")
            .WithSummary("Define a senha pelo link do convite e confirma o e-mail")
            .WithDescription("O token vem no link enviado por e-mail. Vale uma vez e expira (TEN-02, AUT-07).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        group.MapPost("esqueci-senha", async (EsqueciSenhaRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SolicitarRedefinicaoSenhaCommand(request.Email ?? string.Empty), ct))
                    .Match(Results.NoContent))
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Autenticacao)
            .WithName("EsqueciSenha")
            .WithSummary("Envia o link para redefinir a senha")
            .WithDescription("Responde 204 sempre, mesmo se o e-mail não tiver conta, para não revelar quem é cadastrado. "
                + "O link vai para quem pode entrar no endereço acessado (AUT-05).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("redefinir-senha", async (DefinirSenhaRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RedefinirSenhaCommand(request.Token ?? string.Empty, request.Senha ?? string.Empty), ct))
                    .Match(Results.NoContent))
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Autenticacao)
            .WithName("RedefinirSenha")
            .WithSummary("Troca a senha pelo link do \"esqueci minha senha\"")
            .WithDescription("O token vale uma vez e expira. A troca encerra as sessões abertas (AUT-05).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("confirmar-email", async (ConfirmarEmailRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ConfirmarEmailCommand(request.Token ?? string.Empty), ct))
                    .Match(Results.NoContent))
            .AllowAnonymous()
            .RequireRateLimiting(PoliticasLimite.Autenticacao)
            .WithName("ConfirmarEmail")
            .WithSummary("Confirma o e-mail pelo link enviado após o cadastro")
            .WithDescription("O token vale uma vez e expira (AUT-05).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPut("senha", async (AlterarSenhaRequest request, ISender sender, HttpContext http, CancellationToken ct) =>
            {
                Result result = await sender.Send(new AlterarSenhaCommand(request.SenhaAtual ?? string.Empty, request.NovaSenha ?? string.Empty), ct);

                // As sessões foram encerradas: o frontend leva o usuário para entrar de novo com a senha nova.
                if (result.IsSuccess)
                {
                    RefreshTokenCookie.Remover(http);
                }

                return result.Match(Results.NoContent);
            })
            .RequireAuthorization()
            .RequireRateLimiting(PoliticasLimite.Autenticacao)
            .WithName("AlterarSenha")
            .WithSummary("O usuário logado troca a própria senha")
            .WithDescription("Confere a senha atual e encerra todas as sessões da conta (AUT-06).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked);

        group.MapGet("termos", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new SituacaoTermosQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization()
            .WithName("SituacaoTermos")
            .WithSummary("Versões vigentes dos termos de uso e da política de privacidade e o que o usuário já aceitou")
            .WithDescription("Quando pendente = true, o frontend mostra os textos e pede o aceite (SEG-01).")
            .Produces<SituacaoTermosResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("termos/aceite", async (AceitarTermosRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AceitarTermosCommand(
                    request.VersaoTermosDeUso ?? string.Empty, request.VersaoPoliticaDePrivacidade ?? string.Empty), ct)).Match(Results.Ok))
            .RequireAuthorization()
            .WithName("AceitarTermos")
            .WithSummary("Registra o aceite das versões vigentes, com data e IP")
            .Produces<SituacaoTermosResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("minhas-funerarias", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new MinhasFunerariasQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization()
            .WithName("MinhasFunerarias")
            .WithSummary("Funerárias em que o usuário tem vínculo ativo")
            .Produces<IReadOnlyList<FunerariaVinculadaResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static IResult Sessao(Result<SessaoResponse> result, HttpContext http)
    {
        if (result.IsFailure)
        {
            // Sessão recusada: limpa o cookie para o frontend não insistir com um token inválido.
            RefreshTokenCookie.Remover(http);
            return ApiResults.Problem(result);
        }

        SessaoResponse sessao = result.Value;
        RefreshTokenCookie.Escrever(http, sessao.RefreshToken, sessao.RefreshExpiraEm);
        return Results.Ok(new LoginResponse(sessao.AccessToken, sessao.ExpiraEm, sessao.Usuario));
    }
}

/// <summary>Campos opcionais aqui para a validação devolver as mensagens do card em vez de erro de desserialização.</summary>
public sealed record LoginRequest(string? Email, string? Senha);

/// <summary>Corpo de POST /auth/definir-senha e de POST /auth/redefinir-senha.</summary>
public sealed record DefinirSenhaRequest(string? Token, string? Senha);

/// <summary>Corpo de PUT /auth/senha.</summary>
public sealed record AlterarSenhaRequest(string? SenhaAtual, string? NovaSenha);

/// <summary>Corpo de POST /auth/termos/aceite: as versões que o usuário leu.</summary>
public sealed record AceitarTermosRequest(string? VersaoTermosDeUso, string? VersaoPoliticaDePrivacidade);

/// <summary>Corpo de POST /auth/esqueci-senha.</summary>
public sealed record EsqueciSenhaRequest(string? Email);

/// <summary>Corpo de POST /auth/confirmar-email.</summary>
public sealed record ConfirmarEmailRequest(string? Token);

/// <summary>Resposta do login e da renovação (AUT-02). O refresh token vai só no cookie.</summary>
public sealed record LoginResponse(string AccessToken, DateTime ExpiraEm, UsuarioSessao Usuario);
