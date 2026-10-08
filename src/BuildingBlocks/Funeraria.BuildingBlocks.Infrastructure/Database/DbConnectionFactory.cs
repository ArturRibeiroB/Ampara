using System.Data.Common;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Npgsql;

namespace Funeraria.BuildingBlocks.Infrastructure.Database;

internal sealed class DbConnectionFactory(NpgsqlDataSource dataSource) : IDbConnectionFactory
{
    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default) =>
        await dataSource.OpenConnectionAsync(cancellationToken);
}
