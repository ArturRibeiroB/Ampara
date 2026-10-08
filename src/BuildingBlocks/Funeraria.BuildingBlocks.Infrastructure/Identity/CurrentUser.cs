using System.Security.Claims;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using AppClaims = Funeraria.BuildingBlocks.Application.Authorization.Claims;

namespace Funeraria.BuildingBlocks.Infrastructure.Identity;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? IdUsuario => GetGuid(AppClaims.IdUsuario);

    public Guid? IdTenant => GetGuid(AppClaims.IdTenant);

    public string? Perfil => Principal?.FindFirstValue(AppClaims.Perfil);

    public Guid? IdCliente => GetGuid(AppClaims.IdCliente);

    public Guid? IdTerceiro => GetGuid(AppClaims.IdTerceiro);

    public string? TipoTerceiro => Principal?.FindFirstValue(AppClaims.TipoTerceiro);

    private Guid? GetGuid(string claim) =>
        Guid.TryParse(Principal?.FindFirstValue(claim), out Guid value) ? value : null;
}
