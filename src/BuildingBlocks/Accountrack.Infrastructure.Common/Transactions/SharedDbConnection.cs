using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Accountrack.Infrastructure.Common.Transactions;

/// <summary>
/// One physical database connection shared (per request scope) by the module contexts that take
/// part in atomic cross-module transactions. Sharing a single connection lets those contexts enrol
/// in one local transaction (no MSDTC) — the foundation for <see cref="CrossModuleUnitOfWork"/>.
/// </summary>
public interface ISharedDbConnection
{
    DbConnection Connection { get; }
}

internal sealed class SharedDbConnection : ISharedDbConnection, IAsyncDisposable, IDisposable
{
    private readonly SqlConnection _connection;

    public SharedDbConnection(string connectionString) => _connection = new SqlConnection(connectionString);

    public DbConnection Connection => _connection;

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    public void Dispose() => _connection.Dispose();
}
