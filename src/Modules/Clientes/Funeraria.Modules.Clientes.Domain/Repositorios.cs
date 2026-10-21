using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Clientes.Domain.Dependentes;

namespace Funeraria.Modules.Clientes.Domain;

/// <summary>Consultas restritas à funerária atual pelo filtro global de tenant (TEN-01).</summary>
public interface IClienteRepository
{
    Task<Cliente?> ObterPorIdAsync(Guid idCliente, CancellationToken cancellationToken = default);

    Task<bool> ExisteCpfAsync(string cpf, CancellationToken cancellationToken = default);

    void Adicionar(Cliente cliente);
}

/// <summary>Dependentes removidos (soft delete) nunca são retornados.</summary>
public interface IDependenteRepository
{
    /// <summary>Só retorna o dependente se ele for do titular informado (CLI-02).</summary>
    Task<Dependente?> ObterDoTitularAsync(Guid idDependente, Guid idCliente, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Dependente>> ListarDoTitularAsync(Guid idCliente, CancellationToken cancellationToken = default);

    Task<bool> ExisteCpfDoTitularAsync(Guid idCliente, string cpf, CancellationToken cancellationToken = default);

    void Adicionar(Dependente dependente);
}
