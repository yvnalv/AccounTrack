using System.Data.Common;
using Accountrack.Application.Abstractions.Idempotency;
using Npgsql;

namespace Accountrack.Infrastructure.Common.Idempotency;

/// <summary>
/// PostgreSQL-backed <see cref="IIdempotencyStore"/> (ADR-0021). Uses its own short-lived connection so it
/// never interferes with the shared cross-module transaction. Table lives in the <c>platform</c>
/// schema and is ensured at startup. Identifiers keep the platform-wide PascalCase convention, so
/// they are double-quoted for PostgreSQL.
/// </summary>
public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly string _connectionString;

    public IdempotencyStore(string connectionString) => _connectionString = connectionString;

    public async Task<Guid?> TryGetAsync(string scopedKey, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"ResultId\" FROM platform.\"IdempotencyKeys\" WHERE \"ScopedKey\" = @k";
        cmd.Parameters.AddWithValue("k", scopedKey);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    public async Task SaveAsync(string scopedKey, Guid resultId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        // Idempotent insert: first writer wins; a concurrent duplicate is silently ignored.
        cmd.CommandText = """
            INSERT INTO platform."IdempotencyKeys" ("ScopedKey", "ResultId", "CreatedAtUtc")
            VALUES (@k, @r, now())
            ON CONFLICT ("ScopedKey") DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("k", scopedKey);
        cmd.Parameters.AddWithValue("r", resultId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Guid> WriteInTransactionAsync(
        DbConnection connection, DbTransaction transaction, string scopedKey, Guid resultId, CancellationToken ct)
    {
        // First writer wins. A concurrent duplicate blocks on the tentative unique-index entry until
        // this transaction resolves; ON CONFLICT DO NOTHING then reports 0 rows to the loser.
        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO platform."IdempotencyKeys" ("ScopedKey", "ResultId", "CreatedAtUtc")
            VALUES (@k, @r, now())
            ON CONFLICT ("ScopedKey") DO NOTHING;
            """;
        insert.Parameters.Add(new NpgsqlParameter("k", scopedKey));
        insert.Parameters.Add(new NpgsqlParameter("r", resultId));

        var rows = await insert.ExecuteNonQueryAsync(ct);
        if (rows == 1)
        {
            return resultId;
        }

        // Lost the race: the key already exists (committed by the winner). Return its stored id so the
        // caller can roll back its own effects and hand back the original result.
        await using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT \"ResultId\" FROM platform.\"IdempotencyKeys\" WHERE \"ScopedKey\" = @k";
        select.Parameters.Add(new NpgsqlParameter("k", scopedKey));
        var existing = await select.ExecuteScalarAsync(ct);
        return existing is Guid g ? g : resultId;
    }

    /// <summary>Creates the <c>platform.IdempotencyKeys</c> table if it does not exist (startup).</summary>
    public static async Task EnsureTableAsync(string connectionString, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE SCHEMA IF NOT EXISTS platform;
            CREATE TABLE IF NOT EXISTS platform."IdempotencyKeys" (
                "ScopedKey" text NOT NULL CONSTRAINT "PK_IdempotencyKeys" PRIMARY KEY,
                "ResultId" uuid NOT NULL,
                "CreatedAtUtc" timestamptz NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
