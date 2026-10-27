namespace Funeraria.Modules.Contratos.Contracts;

/// <summary>API pública do módulo Contratos para os outros módulos.</summary>
public interface IContratosApi
{
    /// <summary>Se o dependente está coberto por algum contrato não cancelado (CLI-02: impede a remoção).</summary>
    Task<bool> DependentePossuiContratoAtivoAsync(Guid idDependente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Se a pessoa coberta pode usar o plano hoje (CON-04). Usado no registro do beneficiário. Nulo se a pessoa
    /// não existir na funerária atual.
    /// </summary>
    Task<ElegibilidadeDto?> VerificarElegibilidadeAsync(Guid idPessoaCoberta, CancellationToken cancellationToken = default);
}

/// <param name="IdPessoaCoberta">Pessoa no contrato.</param>
/// <param name="IdContrato">Contrato da pessoa.</param>
/// <param name="IdCliente">Titular do contrato.</param>
/// <param name="IdDependente">Preenchido quando a pessoa é um dependente.</param>
/// <param name="Elegivel">Pode usar o plano hoje.</param>
/// <param name="Motivo">Por que não pode.</param>
/// <param name="CarenciaCumpridaEm">Fim da carência da pessoa.</param>
public sealed record ElegibilidadeDto(
    Guid IdPessoaCoberta,
    Guid IdContrato,
    Guid IdCliente,
    Guid? IdDependente,
    bool Elegivel,
    string? Motivo,
    DateOnly CarenciaCumpridaEm);
