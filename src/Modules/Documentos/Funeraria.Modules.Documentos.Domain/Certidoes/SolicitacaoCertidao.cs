using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Estados;
using Funeraria.BuildingBlocks.Domain.Events;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Documentos.Domain.Certidoes;

/// <summary>
/// Pedido do registro do óbito e da certidão (CER-03, CER-04). No modo manual (DEC-04), um funcionário ou um despachante
/// parceiro conduz e registra cada etapa; a certidão emitida fica como documento do beneficiário.
/// </summary>
public sealed class SolicitacaoCertidao : AgregadoComStatus<StatusCertidao, AcaoCertidao>, ITenantEntity, IAuditableEntity
{
    private SolicitacaoCertidao(Guid id, Guid tenantId)
        : base(id, StatusCertidao.Solicitada)
    {
        TenantId = tenantId;
    }

    private SolicitacaoCertidao()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid IdBeneficiario { get; private set; }

    /// <summary>Titular do contrato, avisado quando a certidão sai.</summary>
    public Guid IdCliente { get; private set; }

    public string NomeFalecido { get; private set; } = null!;

    public ModoCertidao Modo { get; private set; }

    /// <summary>Até quando o óbito precisa ser registrado (CER-03).</summary>
    public DateOnly PrazoRegistro { get; private set; }

    public TipoResponsavel? TipoDoResponsavel { get; private set; }

    public Guid? IdUsuarioResponsavel { get; private set; }

    public Guid? IdTerceiroResponsavel { get; private set; }

    public string? NomeResponsavel { get; private set; }

    /// <summary>Atribuição no portal do despachante (TER-02).</summary>
    public Guid? IdAtribuicao { get; private set; }

    /// <summary>Protocolo no cartório ou no e-Óbito.</summary>
    public string? Protocolo { get; private set; }

    /// <summary>Pendência devolvida pelo cartório (REJEITADA) ou o erro (ERRO).</summary>
    public string? Pendencia { get; private set; }

    /// <summary>Número da matrícula da certidão emitida.</summary>
    public string? NumeroMatricula { get; private set; }

    /// <summary>PDF da certidão, guardado como documento do beneficiário (CERTIDAO_OBITO).</summary>
    public Guid? IdDocumentoCertidao { get; private set; }

    protected override MaquinaDeEstados<StatusCertidao, AcaoCertidao> Maquina => EstadosCertidao.Maquina;

    public bool Emitida => Status == StatusCertidao.Emitida;

    public static SolicitacaoCertidao Solicitar(
        Guid tenantId, Guid idBeneficiario, Guid idCliente, string nomeFalecido, ModoCertidao modo, DateOnly prazoRegistro, DateTime agora, Guid? idUsuario)
    {
        var solicitacao = new SolicitacaoCertidao(Guid.CreateVersion7(), tenantId)
        {
            IdBeneficiario = idBeneficiario,
            IdCliente = idCliente,
            NomeFalecido = nomeFalecido,
            Modo = modo,
            PrazoRegistro = prazoRegistro
        };

        solicitacao.RegistrarInicio(agora, idUsuario);
        return solicitacao;
    }

    /// <returns>A atribuição do despachante anterior, para encerrar no portal dele.</returns>
    public Result<Guid?> AtribuirAFuncionario(Guid idUsuario, string nome) => Atribuir(TipoResponsavel.Funcionario, idUsuario, null, nome);

    /// <returns>A atribuição do despachante anterior, para encerrar no portal dele.</returns>
    public Result<Guid?> AtribuirATerceiro(Guid idTerceiro, string nome) => Atribuir(TipoResponsavel.Terceiro, null, idTerceiro, nome);

    public void VincularAtribuicao(Guid idAtribuicao) => IdAtribuicao = idAtribuicao;

    /// <summary>O despachante recusou no portal: a equipe escolhe outro responsável.</summary>
    public void LiberarResponsavel()
    {
        TipoDoResponsavel = null;
        IdUsuarioResponsavel = null;
        IdTerceiroResponsavel = null;
        NomeResponsavel = null;
        IdAtribuicao = null;
    }

    /// <summary>
    /// Etapas registradas pelo responsável (CER-04): EM_PROCESSAMENTO (enviada ao cartório, com o protocolo), REJEITADA (com a
    /// pendência), ERRO ou SOLICITADA (reenviar). EMITIDA só com o PDF e a matrícula, em <see cref="RegistrarEmissao"/>.
    /// </summary>
    public Result MudarStatus(StatusCertidao destino, DateTime agora, Guid? idUsuario, string? observacao, string? protocolo)
    {
        AcaoCertidao? acao = destino switch
        {
            StatusCertidao.EmProcessamento => AcaoCertidao.EnviarParaApi,
            StatusCertidao.Rejeitada => AcaoCertidao.Rejeitar,
            StatusCertidao.Erro => AcaoCertidao.Falhar,
            StatusCertidao.Solicitada => AcaoCertidao.Reenviar,
            _ => null
        };
        if (acao is null)
        {
            return CertidaoErros.EmissaoSoComArquivo;
        }

        if (destino is StatusCertidao.Rejeitada or StatusCertidao.Erro && string.IsNullOrWhiteSpace(observacao))
        {
            return CertidaoErros.PendenciaObrigatoria;
        }

        Result mudou = Executar(acao.Value, agora, idUsuario, observacao);
        if (mudou.IsFailure)
        {
            return mudou;
        }

        if (!string.IsNullOrWhiteSpace(protocolo))
        {
            Protocolo = protocolo.Trim();
        }

        Pendencia = destino is StatusCertidao.Rejeitada or StatusCertidao.Erro ? observacao!.Trim() : null;
        return Result.Success();
    }

    public Result RegistrarEmissao(string numeroMatricula, Guid idDocumento, DateTime agora, Guid? idUsuario)
    {
        Result emitida = Executar(AcaoCertidao.ConfirmarEmissao, agora, idUsuario, $"Matrícula {numeroMatricula.Trim()}");
        if (emitida.IsSuccess)
        {
            NumeroMatricula = numeroMatricula.Trim();
            IdDocumentoCertidao = idDocumento;
            Pendencia = null;
        }

        return emitida;
    }

    protected override void AoMudarStatus(StatusCertidao anterior, StatusCertidao atual, AcaoCertidao acao)
    {
        if (atual == StatusCertidao.Emitida)
        {
            Raise(new CertidaoEmitidaDomainEvent(Id));
        }
    }

    private Result<Guid?> Atribuir(TipoResponsavel tipo, Guid? idUsuario, Guid? idTerceiro, string nome)
    {
        if (Emitida)
        {
            return CertidaoErros.JaEmitida;
        }

        Guid? anterior = IdAtribuicao;
        TipoDoResponsavel = tipo;
        IdUsuarioResponsavel = idUsuario;
        IdTerceiroResponsavel = idTerceiro;
        NomeResponsavel = nome;
        IdAtribuicao = null;
        return anterior;
    }
}

public enum ModoCertidao
{
    Manual = 1,
    Simulado = 2
}

public enum TipoResponsavel
{
    Funcionario = 1,
    Terceiro = 2
}

public sealed record CertidaoEmitidaDomainEvent(Guid IdSolicitacao) : DomainEvent;

public static class CertidaoErros
{
    public static readonly Error NaoEncontrada =
        Error.NaoEncontrado("Certidao.NaoEncontrada", "Não há solicitação de certidão para este beneficiário.");

    public static readonly Error EmAndamento =
        Error.Conflito("Certidao.EmAndamento", "Já existe uma solicitação de certidão para este beneficiário.");

    public static Error DocumentosFaltando(IEnumerable<string> faltando) =>
        Error.Conflito("Certidao.DocumentosFaltando", $"Anexe os documentos obrigatórios antes de pedir a certidão: {string.Join(", ", faltando)}.");

    public static readonly Error JaEmitida =
        Error.Conflito("Certidao.JaEmitida", "A certidão já foi emitida.");

    public static readonly Error EmissaoSoComArquivo =
        Error.Validacao("Certidao.EmissaoSoComArquivo", "Para EMITIDA, registre a certidão com o PDF e a matrícula. Use EM_PROCESSAMENTO, REJEITADA, ERRO ou SOLICITADA.");

    public static readonly Error PendenciaObrigatoria =
        Error.Validacao("Certidao.PendenciaObrigatoria", "Informe a pendência ou o erro na observação.");

    public static readonly Error ResponsavelInvalido =
        Error.Validacao("Certidao.ResponsavelInvalido",
            "O responsável precisa ser gestor ou funcionário ativo da funerária, ou um terceiro CARTORIO_DESPACHANTE ativo.");
}

public interface ISolicitacaoCertidaoRepository
{
    Task<SolicitacaoCertidao?> ObterAsync(Guid idSolicitacao, CancellationToken cancellationToken = default);

    /// <summary>A solicitação mais recente do beneficiário.</summary>
    Task<SolicitacaoCertidao?> ObterDoBeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken = default);

    /// <summary>Solicitações ainda não emitidas de todas as funerárias com o prazo até a data (job de alerta, sem tenant).</summary>
    Task<IReadOnlyList<SolicitacaoCertidao>> ListarComPrazoAteAsync(DateOnly data, CancellationToken cancellationToken = default);

    void Adicionar(SolicitacaoCertidao solicitacao);
}
