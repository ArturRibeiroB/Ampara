namespace Funeraria.Modules.Tenants.Domain.Funerarias;

public interface IFunerariaRepository
{
    Task<Funeraria?> ObterPorIdAsync(Guid idTenant, CancellationToken cancellationToken = default);

    Task<bool> ExisteCnpjAsync(string cnpj, CancellationToken cancellationToken = default);

    Task<bool> ExisteSlugAsync(string slug, CancellationToken cancellationToken = default);

    void Adicionar(Funeraria funeraria);
}
