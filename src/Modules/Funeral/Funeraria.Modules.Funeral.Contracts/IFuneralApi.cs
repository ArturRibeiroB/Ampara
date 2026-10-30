namespace Funeraria.Modules.Funeral.Contracts;

/// <summary>API pública do módulo Funeral para os outros módulos. Respeita o tenant da execução atual.</summary>
public interface IFuneralApi
{
    /// <summary>Preferências registradas pelo titular, para a exportação dos dados dele (SEG-01).</summary>
    Task<IReadOnlyList<PreferenciaExportadaDto>> ListarPreferenciasDoTitularAsync(Guid idCliente, CancellationToken cancellationToken = default);
}

/// <summary>Preferência de funeral como o titular registrou. Tipo: TITULAR ou DEPENDENTE.</summary>
public sealed record PreferenciaExportadaDto(
    Guid IdPessoa,
    string Tipo,
    string Destinacao,
    string TipoCerimonia,
    string? CrencaReligiosa,
    IReadOnlyList<string> Musicas,
    string? Mensagens,
    string? Observacoes,
    IReadOnlyList<Guid> IdsItensCatalogo);
