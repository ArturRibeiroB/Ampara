namespace Funeraria.Modules.Identidade.Domain.Vinculos;

public interface IVinculoRepository
{
    Task<UsuarioVinculo?> ObterAsync(Guid idUsuario, Guid idTenant, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsuarioVinculo>> ListarAtivosDoUsuarioAsync(Guid idUsuario, CancellationToken cancellationToken = default);

    void Adicionar(UsuarioVinculo vinculo);
}
