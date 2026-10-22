namespace Funeraria.Modules.Clientes.Contracts;

/// <summary>
/// API pública do módulo Clientes para os outros módulos (ex.: Contratos confere titular e dependentes na contratação).
/// Respeita o tenant da execução atual; dependentes removidos não aparecem.
/// </summary>
public interface IClientesApi
{
    Task<TitularDto?> ObterTitularAsync(Guid idCliente, CancellationToken cancellationToken = default);

    /// <summary>Titulares com os ids informados. Ids sem cliente ficam de fora.</summary>
    Task<IReadOnlyList<TitularDto>> ObterTitularesAsync(IReadOnlyCollection<Guid> idsCliente, CancellationToken cancellationToken = default);

    /// <summary>Titular pelo CPF (só dígitos), dentro da funerária atual.</summary>
    Task<TitularDto?> ObterTitularPorCpfAsync(string cpf, CancellationToken cancellationToken = default);

    /// <summary>Dependentes com os ids informados, de qualquer titular da funerária. Ids sem dependente ficam de fora.</summary>
    Task<IReadOnlyList<DependenteDto>> ObterDependentesAsync(IReadOnlyCollection<Guid> idsDependente, CancellationToken cancellationToken = default);
}

public sealed record TitularDto(Guid IdCliente, string Nome, string Cpf);

public sealed record DependenteDto(Guid IdDependente, Guid IdCliente, string Nome, string Cpf);
