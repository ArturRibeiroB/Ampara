namespace Funeraria.Modules.Funeral.Contracts;

/// <summary>API pública do módulo Funeral para os outros módulos. Respeita o tenant da execução atual.</summary>
public interface IFuneralApi
{
    /// <summary>Preferências registradas pelo titular, para a exportação dos dados dele (SEG-01).</summary>
    Task<IReadOnlyList<PreferenciaExportadaDto>> ListarPreferenciasDoTitularAsync(Guid idCliente, CancellationToken cancellationToken = default);

    /// <summary>Beneficiário (falecido) da funerária atual, para documentos e certidão (CER). Nulo se não existir.</summary>
    Task<BeneficiarioDto?> ObterBeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken = default);

    /// <summary>A pessoa coberta já foi registrada como beneficiária (falecida): não sai do contrato (CON-02).</summary>
    Task<bool> PessoaEhBeneficiariaAsync(Guid idPessoaCoberta, CancellationToken cancellationToken = default);

    /// <summary>Há funeral do contrato ainda não encerrado: o contrato não pode ser cancelado (CON-03).</summary>
    Task<bool> PossuiFuneralEmAndamentoAsync(Guid idContrato, CancellationToken cancellationToken = default);
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

/// <param name="IdBeneficiario">Beneficiário.</param>
/// <param name="IdCliente">Titular do contrato, que acompanha (DEC-06).</param>
/// <param name="Nome">Nome do falecido.</param>
/// <param name="DataNascimento">Data de nascimento.</param>
/// <param name="DataFalecimento">Data do falecimento (conta o prazo do registro do óbito).</param>
/// <param name="LocalFalecimento">Onde faleceu.</param>
/// <param name="NomeResponsavel">Declarante/responsável pela família.</param>
/// <param name="TelefoneResponsavel">Telefone do responsável.</param>
public sealed record BeneficiarioDto(
    Guid IdBeneficiario,
    Guid IdCliente,
    string Nome,
    DateOnly DataNascimento,
    DateOnly DataFalecimento,
    string LocalFalecimento,
    string NomeResponsavel,
    string TelefoneResponsavel);
