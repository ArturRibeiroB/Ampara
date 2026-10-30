using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Documentos.Domain.Documentos;

/// <summary>
/// Arquivo anexado ao beneficiário (CER-01): declaração de óbito, documentos do falecido e do declarante, comprovantes e a
/// certidão emitida (CER-04). O arquivo fica no armazenamento privado (INF-18); aqui ficam os dados e a chave.
/// </summary>
public sealed class Documento : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private Documento(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private Documento()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdBeneficiario { get; private set; }

    public TipoDocumento Tipo { get; private set; }

    public string NomeOriginal { get; private set; } = null!;

    /// <summary>Chave no armazenamento de arquivos. Não sai na API: o download é por link temporário.</summary>
    public string Chave { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long Tamanho { get; private set; }

    public DateTime EnviadoEm { get; private set; }

    public static Documento Anexar(
        Guid tenantId, Guid idBeneficiario, TipoDocumento tipo, string nomeOriginal, string chave, string contentType, long tamanho, DateTime agora) =>
        new(Guid.CreateVersion7(), tenantId)
        {
            IdBeneficiario = idBeneficiario,
            Tipo = tipo,
            NomeOriginal = Path.GetFileName(nomeOriginal),
            Chave = chave,
            ContentType = contentType,
            Tamanho = tamanho,
            EnviadoEm = agora
        };
}

public enum TipoDocumento
{
    DeclaracaoObito = 1,
    DocumentoFalecido = 2,
    DocumentoDeclarante = 3,
    ComprovanteResidencia = 4,
    CertidaoCivil = 5,
    CertidaoObito = 6,
    Outro = 7
}

public static class DocumentoErros
{
    public static readonly Error NaoEncontrado = Error.NaoEncontrado("Documento.NaoEncontrado", "Documento não encontrado.");

    public static readonly Error BeneficiarioNaoEncontrado =
        Error.NaoEncontrado("Documento.BeneficiarioNaoEncontrado", "Beneficiário não encontrado.");

    /// <summary>Documentos do falecido: só a equipe interna e o titular do contrato (CER-01).</summary>
    public static readonly Error AcessoNegado =
        Error.Proibido("Documento.AcessoNegado", "Os documentos do falecido são só da equipe interna e do titular do contrato.");

    public static readonly Error CertidaoEmitidaNaoSaiPorAqui =
        Error.Conflito("Documento.CertidaoEmitida", "A certidão emitida faz parte do registro da certidão e não é excluída por aqui.");
}

public interface IDocumentoRepository
{
    Task<Documento?> ObterAsync(Guid idDocumento, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Documento>> ListarDoBeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken = default);

    void Adicionar(Documento documento);

    void Remover(Documento documento);
}
