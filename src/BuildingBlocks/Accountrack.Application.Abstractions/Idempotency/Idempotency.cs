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
/// Stores the result id produced for an idempotency key, so a replay returns it instead of
/// re-executing. Keys are pre-scoped (tenant + command) by the caller.
/// </summary>
public interface IIdempotencyStore
{
    Task<Guid?> TryGetAsync(string scopedKey, CancellationToken ct);
    Task SaveAsync(string scopedKey, Guid resultId, CancellationToken ct);
}
