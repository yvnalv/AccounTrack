using Accountrack.Application.Abstractions.Integration;
using Npgsql;

namespace Accountrack.Infrastructure.Common.Outbox;

/// <summary>
/// PostgreSQL-backed <see cref="IInboxStore"/>: records that a handler has applied a given event, so the
/// at-least-once outbox does not double-apply non-idempotent consumers (ADR-0007). Uses its own
/// short-lived connection; the table lives in the <c>platform</c> schema and is ensured at startup.
/// Identifiers keep the platform-wide PascalCase convention, so they are double-quoted for PostgreSQL.
/// </summary>
public sealed class InboxStore : IInboxStore
{
    private readonly string _connectionString;

    public InboxStore(string connectionString) => _connectionString = connectionString;

    public async Task<bool> HasProcessedAsync(string handler, Guid eventId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM platform.\"InboxState\" WHERE \"Handler\" = @h AND \"EventId\" = @e";
        cmd.Parameters.AddWithValue("h", handler);
        cmd.Parameters.AddWithValue("e", eventId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    public async Task MarkProcessedAsync(string handler, Guid eventId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        // First writer wins; a concurrent duplicate is silently ignored.
        cmd.CommandText = """
            INSERT INTO platform."InboxState" ("Handler", "EventId", "ProcessedAtUtc")
            VALUES (@h, @e, now())
            ON CONFLICT ("Handler", "EventId") DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("h", handler);
        cmd.Parameters.AddWithValue("e", eventId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Creates the <c>platform.InboxState</c> table if it does not exist (startup).</summary>
    public static async Task EnsureTableAsync(string connectionString, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE SCHEMA IF NOT EXISTS platform;
            CREATE TABLE IF NOT EXISTS platform."InboxState" (
                "Handler" text NOT NULL,
                "EventId" uuid NOT NULL,
                "ProcessedAtUtc" timestamptz NOT NULL,
                CONSTRAINT "PK_InboxState" PRIMARY KEY ("Handler", "EventId")
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
