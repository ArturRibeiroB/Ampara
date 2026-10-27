using Funeraria.BuildingBlocks.Domain.Entities;

namespace Funeraria.Modules.Identidade.Domain.Termos;

/// <summary>
/// Aceite de um documento legal numa versão (SEG-01, LGPD): quem, qual documento, qual versão, quando e de onde.
/// Só é inserido; um aceite novo não apaga o anterior, para o histórico provar o que a pessoa aceitou em cada época.
/// </summary>
public sealed class AceiteTermos : Entity<Guid>
{
    private AceiteTermos(Guid id)
        : base(id)
    {
    }

    private AceiteTermos()
    {
    }

    public Guid IdUsuario { get; private set; }

    public DocumentoLegal Documento { get; private set; }

    public string Versao { get; private set; } = null!;

    public DateTime AceitoEm { get; private set; }

    public string? Ip { get; private set; }

    public static AceiteTermos Registrar(Guid idUsuario, DocumentoLegal documento, string versao, DateTime agora, string? ip) =>
        new(Guid.CreateVersion7()) { IdUsuario = idUsuario, Documento = documento, Versao = versao, AceitoEm = agora, Ip = ip };
}

public enum DocumentoLegal
{
    TermosDeUso = 1,
    PoliticaDePrivacidade = 2
}

public interface IAceiteTermosRepository
{
    Task<IReadOnlyList<AceiteTermos>> ListarDoUsuarioAsync(Guid idUsuario, CancellationToken cancellationToken = default);

    void Adicionar(AceiteTermos aceite);
}
