using Accountrack.Application.Abstractions.Integration;
using Microsoft.Data.SqlClient;

namespace Accountrack.Infrastructure.Common.Outbox;

/// <summary>
/// SQL-backed <see cref="IInboxStore"/>: records that a handler has applied a given event, so the
/// at-least-once outbox does not double-apply non-idempotent consumers (ADR-0007). Uses its own
/// short-lived connection; the table lives in the <c>platform</c> schema and is ensured at startup.
/// </summary>
public sealed class InboxStore : IInboxStore
{
    private readonly string _connectionString;

    public InboxStore(string connectionString) => _connectionString = connectionString;

    public async Task<bool> HasProcessedAsync(string handler, Guid eventId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM platform.InboxState WHERE Handler = @h AND EventId = @e";
        cmd.Parameters.AddWithValue("@h", handler);
        cmd.Parameters.AddWithValue("@e", eventId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    public async Task MarkProcessedAsync(string handler, Guid eventId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        // First writer wins; a concurrent duplicate is silently ignored.
        cmd.CommandText = """
            INSERT INTO platform.InboxState (Handler, EventId, ProcessedAtUtc)
            SELECT @h, @e, SYSUTCDATETIME()
            WHERE NOT EXISTS (SELECT 1 FROM platform.InboxState WHERE Handler = @h AND EventId = @e);
            """;
        cmd.Parameters.AddWithValue("@h", handler);
        cmd.Parameters.AddWithValue("@e", eventId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Creates the <c>platform.InboxState</c> table if it does not exist (startup).</summary>
    public static async Task EnsureTableAsync(string connectionString, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            IF SCHEMA_ID('platform') IS NULL EXEC('CREATE SCHEMA platform');
            IF OBJECT_ID('platform.InboxState') IS NULL
            CREATE TABLE platform.InboxState (
                Handler nvarchar(256) NOT NULL,
                EventId uniqueidentifier NOT NULL,
                ProcessedAtUtc datetime2 NOT NULL,
                CONSTRAINT PK_InboxState PRIMARY KEY (Handler, EventId)
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
