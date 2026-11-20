namespace Funeraria.BuildingBlocks.Application.Abstractions;

/// <summary>
/// O banco recusou a gravação por um índice único ou uma restrição de exclusão: outra requisição gravou o mesmo dado
/// (ou um horário que se sobrepõe) ao mesmo tempo.
/// Os handlers verificam a duplicidade antes de gravar; esta exceção cobre a corrida entre duas gravações.
/// Lançada pelo <see cref="IUnitOfWork"/>, para a camada Application não depender do EF Core nem do Npgsql.
/// </summary>
public sealed class ViolacaoDeUnicidadeException(string? restricao, Exception inner)
    : Exception($"Violação do índice único {restricao}.", inner)
{
    /// <summary>Nome do índice no banco (ex.: ix_usuario_vinculos_id_usuario_id_tenant).</summary>
    public string? Restricao { get; } = restricao;
}

/// <summary>
/// O banco abortou a gravação por um impasse (deadlock) com outra transação que disputava os mesmos dados. A outra seguiu; esta
/// perdeu a disputa. Acontece, por exemplo, com duas reservas simultâneas na restrição de exclusão (FUN-05).
/// </summary>
public sealed class ImpasseNoBancoException(Exception inner)
    : Exception("A gravação perdeu a disputa com outra transação (deadlock).", inner);
