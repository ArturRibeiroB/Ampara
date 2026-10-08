using System.Data.Common;

namespace Funeraria.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Conexão para consultas de leitura com Dapper (INF-04).
/// Atenção: SQL escrito à mão não passa pelo filtro de tenant do EF Core.
/// Toda query precisa filtrar <c>id_tenant</c> e só ler o schema do próprio módulo.
/// </summary>
public interface IDbConnectionFactory
{
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
