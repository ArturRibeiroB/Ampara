using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Identidade.Application.Commands.EncerrarSessao;
using Funeraria.Modules.Identidade.Application.Commands.Login;
using Funeraria.Modules.Identidade.Application.Commands.RenovarSessao;
using Funeraria.Modules.Identidade.Application.Queries.MinhasFunerarias;
using Funeraria.Modules.Identidade.Application.Sessoes;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Identidade.Endpoints.Auth;

/// <summary>Login, renovação, logout e funerárias do usuário (AUT-02, AUT-03).</summary>
internal sealed class AuthEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("auth").WithTags("Autenticação");

        group.MapPost("login", async (LoginRequest request, ISender sender, HttpContext http, CancellationToken ct) =>
                Sessao(await sender.Send(new LoginCommand(request.Email ?? string.Empty, request.Senha ?? string.Empty), ct), http))
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Entra na funerária do endereço acessado (ou na plataforma, para o ADMIN)")
            .WithDescription("Devolve o access token no corpo e o refresh token em cookie httpOnly.")
            .Produces<LoginResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status423Locked);

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

/// <summary>Resposta do login e da renovação (AUT-02). O refresh token vai só no cookie.</summary>
public sealed record LoginResponse(string AccessToken, DateTime ExpiraEm, UsuarioSessao Usuario);
