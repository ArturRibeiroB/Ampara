namespace Funeraria.Modules.Identidade.Domain.Vinculos;

public interface IVinculoRepository
{
    Task<UsuarioVinculo?> ObterAsync(Guid idUsuario, Guid idTenant, CancellationToken cancellationToken = default);

    /// <summary>Todos os vínculos da conta, ativos ou não, em todas as funerárias.</summary>
    Task<IReadOnlyList<UsuarioVinculo>> ListarDoUsuarioAsync(Guid idUsuario, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsuarioVinculo>> ListarAtivosDoUsuarioAsync(Guid idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Gestores (FUNERARIA) com vínculo ativo na funerária (AUT-08: o último não sai).</summary>
    Task<IReadOnlyList<Guid>> ListarGestoresAtivosAsync(Guid idTenant, CancellationToken cancellationToken = default);

    Task<int> ContarGestoresAtivosAsync(Guid idTenant, CancellationToken cancellationToken = default);

    Task<UsuarioVinculo?> ObterPorTerceiroAsync(Guid idTenant, Guid idTerceiro, CancellationToken cancellationToken = default);

    void Adicionar(UsuarioVinculo vinculo);
}
