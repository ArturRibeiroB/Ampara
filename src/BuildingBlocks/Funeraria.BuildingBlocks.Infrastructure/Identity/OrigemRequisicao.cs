using Funeraria.BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Funeraria.BuildingBlocks.Infrastructure.Identity;

internal sealed class OrigemRequisicao(IHttpContextAccessor http) : IOrigemRequisicao
{
    public string? Ip => http.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
