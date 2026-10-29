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

    /// <summary>
    /// Plano do contrato que cobre a pessoa hoje (titular, ou o dependente informado), para mostrar o que está coberto
    /// (FUN-02). Com mais de um contrato, vale o ativo mais recente. Nulo se ninguém cobre a pessoa.
    /// </summary>
    Task<Guid?> PlanoQueCobreAsync(Guid idCliente, Guid? idDependente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Titulares da funerária atual com pelo menos um contrato no status informado (<see cref="StatusContratoApi"/>), ou com
    /// qualquer contrato quando o status é nulo. Usado no filtro <c>status_contrato</c> da busca de clientes (CLI-03).
    /// </summary>
    Task<IReadOnlyList<Guid>> ClientesComContratoAsync(string? status, CancellationToken cancellationToken = default);

    /// <summary>Plano do contrato, para calcular o que o funeral tem coberto (FUN-03). Nulo se o contrato não existir.</summary>
    Task<Guid?> PlanoDoContratoAsync(Guid idContrato, CancellationToken cancellationToken = default);

    /// <summary>Contratos do cliente, para a exportação dos dados pelo titular (SEG-01).</summary>
    Task<IReadOnlyList<ContratoDoClienteDto>> ListarDoClienteAsync(Guid idCliente, CancellationToken cancellationToken = default);
}

/// <summary>Status do contrato no formato da API.</summary>
public static class StatusContratoApi
{
    public static readonly IReadOnlyList<string> Todos = ["ATIVO", "A_PAGAR", "SUSPENSO", "CANCELADO"];
}

/// <summary>Contrato do cliente. Status: ATIVO, A_PAGAR, SUSPENSO ou CANCELADO; periodicidade: MENSAL ou ANUAL.</summary>
public sealed record ContratoDoClienteDto(
    Guid IdContrato,
    string NomePlano,
    string Status,
    string Periodicidade,
    decimal ValorTotal,
    DateOnly DataContratacao,
    DateOnly? DataFim,
    int QuantidadePessoas);

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
