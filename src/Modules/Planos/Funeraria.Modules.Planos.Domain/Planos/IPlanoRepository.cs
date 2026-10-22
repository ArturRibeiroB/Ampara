namespace Funeraria.Modules.Planos.Domain.Planos;

public interface IPlanoRepository
{
    Task<Plano?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExisteComNomeAsync(string nome, Guid? exceto = null, CancellationToken cancellationToken = default);

    /// <summary>Se a funerária atual já tem algum plano, ativo ou não.</summary>
    Task<bool> ExisteAlgumAsync(CancellationToken cancellationToken = default);

    void Adicionar(Plano plano);
}
