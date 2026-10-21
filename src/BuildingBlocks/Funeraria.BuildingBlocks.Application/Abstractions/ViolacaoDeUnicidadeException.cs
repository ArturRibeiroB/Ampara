namespace Funeraria.BuildingBlocks.Application.Abstractions;

/// <summary>
/// O banco recusou a gravação por um índice único: outra requisição gravou o mesmo dado ao mesmo tempo.
/// Os handlers verificam a duplicidade antes de gravar; esta exceção cobre a corrida entre duas gravações.
/// Lançada pelo <see cref="IUnitOfWork"/>, para a camada Application não depender do EF Core nem do Npgsql.
/// </summary>
public sealed class ViolacaoDeUnicidadeException(string? restricao, Exception inner)
    : Exception($"Violação do índice único {restricao}.", inner)
{
    /// <summary>Nome do índice no banco (ex.: ix_usuario_vinculos_id_usuario_id_tenant).</summary>
    public string? Restricao { get; } = restricao;
}
