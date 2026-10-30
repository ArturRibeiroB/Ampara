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

    /// <summary>Dependentes ativos do titular, por nome.</summary>
    Task<IReadOnlyList<DependenteDto>> ListarDependentesDoTitularAsync(Guid idCliente, CancellationToken cancellationToken = default);

    /// <summary>Dependentes com os ids informados, de qualquer titular da funerária. Ids sem dependente ficam de fora.</summary>
    Task<IReadOnlyList<DependenteDto>> ObterDependentesAsync(IReadOnlyCollection<Guid> idsDependente, CancellationToken cancellationToken = default);
}

/// <summary>Titular do contrato. <c>IdUsuario</c> é a conta dele na Identidade, para notificar a família (FUN-06).</summary>
public sealed record TitularDto(Guid IdCliente, string Nome, string Cpf, DateOnly DataNascimento, Guid IdUsuario, string Email);

public sealed record DependenteDto(Guid IdDependente, Guid IdCliente, string Nome, string Cpf, DateOnly DataNascimento);
