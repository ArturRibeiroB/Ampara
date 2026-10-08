namespace Funeraria.BuildingBlocks.Application.Abstractions;

/// <summary>Usuário autenticado da requisição, lido das claims do JWT (AUT-04).</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? IdUsuario { get; }

    Guid? IdTenant { get; }

    string? Perfil { get; }

    Guid? IdCliente { get; }

    Guid? IdTerceiro { get; }

    string? TipoTerceiro { get; }
}
