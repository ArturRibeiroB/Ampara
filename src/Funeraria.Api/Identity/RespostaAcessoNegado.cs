using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Funeraria.Api.Identity;

/// <summary>
/// 403 com a mensagem do card quando o endpoint declara <see cref="MensagemAcessoNegado"/>; nos demais, o comportamento
/// padrão do ASP.NET (ProblemDetails genérico).
/// </summary>
internal sealed class RespostaAcessoNegado(IProblemDetailsService problemDetails) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _padrao = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.GetEndpoint()?.Metadata.GetMetadata<MensagemAcessoNegado>() is { } mensagem)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Acesso negado",
                    Detail = mensagem.Mensagem,
                    Extensions = { ["code"] = "Auth.AcessoNegado" }
                }
            });
            return;
        }

        await _padrao.HandleAsync(next, context, policy, authorizeResult);
    }
}
