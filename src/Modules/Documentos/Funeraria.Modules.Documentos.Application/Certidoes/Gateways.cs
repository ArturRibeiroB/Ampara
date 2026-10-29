using System.Text;
using Funeraria.Modules.Documentos.Application.Documentos;
using Funeraria.Modules.Documentos.Domain.Certidoes;
using Funeraria.Modules.Tenants.Contracts;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Documentos.Application.Certidoes;

/// <summary>
/// Ponto de integração da certidão de óbito (CER-02, DEC-04). O MVP não depende de API externa: o modo MANUAL deixa o
/// responsável conduzir. Uma integração futura (e-Óbito ou intermediário de segunda via) entra como outra implementação.
/// Nada de dados pessoais nos logs: só ids.
/// </summary>
public interface ICertidaoObitoGateway
{
    ModoCertidao Modo { get; }

    /// <summary>Envia o pedido. No modo manual não faz nada: o responsável protocola no cartório.</summary>
    Task<ResultadoCertidao> SolicitarAsync(SolicitacaoCertidao solicitacao, CancellationToken cancellationToken);

    /// <summary>
    /// Consulta o andamento (para o job de consulta periódica ou o webhook de uma integração real). Nulo quando o modo não
    /// consulta (manual) ou ainda não há novidade.
    /// </summary>
    Task<ResultadoCertidao?> ConsultarAsync(SolicitacaoCertidao solicitacao, CancellationToken cancellationToken);
}

/// <param name="Protocolo">Protocolo do pedido, quando o envio é automático.</param>
/// <param name="NumeroMatricula">Matrícula, quando a certidão já saiu.</param>
/// <param name="Pdf">PDF da certidão emitida.</param>
public sealed record ResultadoCertidao(string? Protocolo, string? NumeroMatricula = null, byte[]? Pdf = null)
{
    public static readonly ResultadoCertidao Manual = new(Protocolo: null);

    public bool Emitida => NumeroMatricula is not null && Pdf is not null;
}

/// <summary>Padrão do MVP (DEC-04): funcionário ou despachante conduz e registra cada etapa.</summary>
public sealed class CertidaoManualGateway : ICertidaoObitoGateway
{
    public ModoCertidao Modo => ModoCertidao.Manual;

    public Task<ResultadoCertidao> SolicitarAsync(SolicitacaoCertidao solicitacao, CancellationToken cancellationToken) =>
        Task.FromResult(ResultadoCertidao.Manual);

    public Task<ResultadoCertidao?> ConsultarAsync(SolicitacaoCertidao solicitacao, CancellationToken cancellationToken) =>
        Task.FromResult<ResultadoCertidao?>(null);
}

/// <summary>Desenvolvimento e testes: "emite" na hora, com protocolo, matrícula e um PDF de exemplo (CER-02).</summary>
public sealed partial class CertidaoSimuladaGateway(ILogger<CertidaoSimuladaGateway> logger) : ICertidaoObitoGateway
{
    public ModoCertidao Modo => ModoCertidao.Simulado;

    public Task<ResultadoCertidao> SolicitarAsync(SolicitacaoCertidao solicitacao, CancellationToken cancellationToken)
    {
        string sufixo = solicitacao.Id.ToString("N")[..10].ToUpperInvariant();
        LogSimulada(logger, solicitacao.Id);
        byte[] pdf = Encoding.ASCII.GetBytes($"%PDF-1.4\n% Certidao simulada {sufixo}\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n");
        return Task.FromResult(new ResultadoCertidao($"SIM-{sufixo}", $"SIMULADA-{sufixo}", pdf));
    }

    public Task<ResultadoCertidao?> ConsultarAsync(SolicitacaoCertidao solicitacao, CancellationToken cancellationToken) =>
        Task.FromResult<ResultadoCertidao?>(null);

    [LoggerMessage(Level = LogLevel.Information, Message = "Certidão simulada emitida para a solicitação {IdSolicitacao}")]
    private static partial void LogSimulada(ILogger logger, Guid idSolicitacao);
}

/// <summary>
/// Escolhe o gateway pelo modo da funerária (TEN-03). O SIMULADO só vale onde a configuração permite
/// (<c>Documentos:PermitirModoSimulado</c>); fora disso a funerária fica no MANUAL.
/// </summary>
public sealed class SeletorGatewayCertidao(IEnumerable<ICertidaoObitoGateway> gateways, ITenantsApi tenants, IConfiguracaoDocumentos configuracao)
{
    public async Task<ICertidaoObitoGateway> ObterAsync(Guid idTenant, CancellationToken cancellationToken)
    {
        bool simulado = configuracao.PermitirModoSimulado && await tenants.ObterModoCertidaoAsync(idTenant, cancellationToken) == "SIMULADO";
        ModoCertidao modo = simulado ? ModoCertidao.Simulado : ModoCertidao.Manual;
        return gateways.Single(g => g.Modo == modo);
    }
}
