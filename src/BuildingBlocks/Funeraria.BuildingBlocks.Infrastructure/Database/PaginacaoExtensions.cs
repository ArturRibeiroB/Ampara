using Funeraria.BuildingBlocks.Application.Paginacao;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.BuildingBlocks.Infrastructure.Database;

/// <summary>Paginação de consultas EF Core no formato padrão da API (INF-17). A consulta já deve vir ordenada.</summary>
public static class PaginacaoExtensions
{
    public static async Task<PaginaResult<T>> ToPaginaAsync<T>(
        this IQueryable<T> consulta,
        PaginaRequest pagina,
        CancellationToken cancellationToken = default)
    {
        PaginaRequest normalizada = pagina.Normalizada();

        long total = await consulta.LongCountAsync(cancellationToken);
        List<T> itens = await consulta
            .Skip(normalizada.Deslocamento)
            .Take(normalizada.Tamanho)
            .ToListAsync(cancellationToken);

        return new PaginaResult<T>(itens, normalizada.Pagina, normalizada.Tamanho, total);
    }
}
