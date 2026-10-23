using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.Modules.Terceiros.Domain.Terceiros;

namespace Funeraria.Modules.Terceiros.Application.Abstractions;

public interface ITerceirosUnitOfWork : IUnitOfWork;

public interface ITerceirosLeitura
{
    /// <summary>Terceiros da funerária atual, por nome.</summary>
    Task<(IReadOnlyList<Terceiro> Itens, long Total)> ListarAsync(
        TipoTerceiro? tipo,
        StatusTerceiro? status,
        PaginaRequest pagina,
        CancellationToken cancellationToken = default);
}
