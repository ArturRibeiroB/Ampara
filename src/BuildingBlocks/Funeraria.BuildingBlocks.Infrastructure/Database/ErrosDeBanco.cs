using Funeraria.BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Funeraria.BuildingBlocks.Infrastructure.Database;

/// <summary>Traduz erros do PostgreSQL em exceções da aplicação.</summary>
public static class ErrosDeBanco
{
    /// <summary>
    /// Grava e converte violação de índice único (SQLSTATE 23505) em <see cref="ViolacaoDeUnicidadeException"/>.
    /// Usado pelo <c>SaveChangesAsync</c> de todos os DbContexts de módulo.
    /// </summary>
    public static async Task<int> SalvarAsync(Func<CancellationToken, Task<int>> salvar, CancellationToken cancellationToken)
    {
        try
        {
            return await salvar(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new ViolacaoDeUnicidadeException(pg.ConstraintName, ex);
        }
    }
}
