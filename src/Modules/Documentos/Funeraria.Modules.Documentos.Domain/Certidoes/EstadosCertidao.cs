using Funeraria.BuildingBlocks.Domain.Estados;

namespace Funeraria.Modules.Documentos.Domain.Certidoes;

public enum StatusCertidao
{
    Solicitada = 1,
    EmProcessamento = 2,
    Emitida = 3,
    Rejeitada = 4,
    Erro = 5
}

public enum AcaoCertidao
{
    /// <summary>No fluxo assistido do MVP (DEC-04), é o responsável protocolar o pedido no cartório ou no e-Óbito.</summary>
    EnviarParaApi = 1,
    ConfirmarEmissao = 2,
    Rejeitar = 3,
    Falhar = 4,
    Reenviar = 5
}

/// <summary>Transições da solicitação de certidão de óbito (ARQ-01, CER-03, CER-04).</summary>
public static class EstadosCertidao
{
    public static readonly MaquinaDeEstados<StatusCertidao, AcaoCertidao> Maquina = new MaquinaDeEstados<StatusCertidao, AcaoCertidao>()
        .Permitir(AcaoCertidao.EnviarParaApi, StatusCertidao.EmProcessamento, StatusCertidao.Solicitada)
        .Permitir(AcaoCertidao.ConfirmarEmissao, StatusCertidao.Emitida, StatusCertidao.EmProcessamento)
        .Permitir(AcaoCertidao.Rejeitar, StatusCertidao.Rejeitada, StatusCertidao.EmProcessamento)
        .Permitir(AcaoCertidao.Falhar, StatusCertidao.Erro, StatusCertidao.EmProcessamento)
        .Permitir(AcaoCertidao.Reenviar, StatusCertidao.Solicitada, StatusCertidao.Erro, StatusCertidao.Rejeitada);
}
