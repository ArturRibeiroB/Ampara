namespace Funeraria.Modules.Identidade.Domain.Vinculos;

public interface IVinculoRepository
{
    Task<UsuarioVinculo?> ObterAsync(Guid idUsuario, Guid idTenant, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsuarioVinculo>> ListarAtivosDoUsuarioAsync(Guid idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Gestores (FUNERARIA) com vínculo ativo na funerária (AUT-08: o último não sai).</summary>
    Task<int> ContarGestoresAtivosAsync(Guid idTenant, CancellationToken cancellationToken = default);

    void Adicionar(UsuarioVinculo vinculo);
}
