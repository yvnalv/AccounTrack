using System.Data.Common;

namespace Accountrack.Application.Abstractions.Idempotency;

/// <summary>
/// Marks a command that must be idempotent when an idempotency key is supplied (ADR-0021): if the
/// same key is replayed (network retry, proxy resend, double submit), the command runs once and the
/// original result is returned. Only meaningful for commands returning <c>Result&lt;Guid&gt;</c>.
/// </summary>
public interface IIdempotentCommand;

/// <summary>The idempotency key for the current request (from the <c>Idempotency-Key</c> header).</summary>
public interface IIdempotencyContext
{
    string? Key { get; }
}

/// <summary>
/// Per-request handshake between the <c>IdempotencyBehavior</c> and the cross-module transaction
/// coordinator (ADR-0021). The behavior publishes the scoped key before the handler runs; the
/// coordinator writes that key inside the <em>same</em> transaction as the business effects and marks
/// it <see cref="Written"/>, so the behavior knows not to fall back to a separate-connection save.
/// This is what makes atomic-posting flows exactly-once: effects and key commit or roll back together.
/// </summary>
public interface IIdempotencyScope
{
    /// <summary>The tenant-scoped key for the in-flight idempotent command, or null when none applies.</summary>
    string? Key { get; }

    /// <summary>Set once the key has been persisted in the business transaction (by the coordinator).</summary>
    bool Written { get; set; }

    /// <summary>Publishes the scoped key for the in-flight command.</summary>
    void Begin(string scopedKey);

    /// <summary>Resets the scope at the end of the request/command.</summary>
    void Clear();
}

/// <summary>
/// Stores the result id produced for an idempotency key, so a replay returns it instead of
/// re-executing. Keys are pre-scoped (tenant + command) by the caller.
/// </summary>
public interface IIdempotencyStore
{
    Task<Guid?> TryGetAsync(string scopedKey, CancellationToken ct);

    /// <summary>
    /// Records the key on its own short-lived connection (the legacy, at-least-once path used for
    /// commands that do not run inside the cross-module transaction).
    /// </summary>
    Task SaveAsync(string scopedKey, Guid resultId, CancellationToken ct);

    /// <summary>
    /// Records the key on the supplied connection/transaction so it commits atomically with the
    /// business effects (exactly-once). Returns the <em>winning</em> result id: <paramref name="resultId"/>
    /// if this caller claimed the key, or the previously stored id if a concurrent/earlier writer won
    /// the race (the caller must then roll back and return that id).
    /// </summary>
    Task<Guid> WriteInTransactionAsync(
        DbConnection connection, DbTransaction transaction, string scopedKey, Guid resultId, CancellationToken ct);
}
