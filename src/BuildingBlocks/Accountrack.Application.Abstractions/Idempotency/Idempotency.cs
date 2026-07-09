using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Application.Abstractions.Idempotency;

/// <summary>
/// Marks a command that must be idempotent when an idempotency key is supplied (ADR-0021): if the
/// same key is replayed (network retry, proxy resend, double submit), the command runs once and the
/// original result is returned. Meaningful for commands returning <c>Result&lt;Guid&gt;</c> or
/// <c>Result&lt;T&gt;</c> where <c>T</c> is an <see cref="IIdempotentResult"/>.
/// </summary>
public interface IIdempotentCommand;

/// <summary>
/// A richer command result addressable by a single <see cref="Guid"/> identity, so the idempotency
/// machinery can store and replay it even though the result is not a bare <c>Guid</c> (ADR-0021).
/// Only that id survives to a replay; every other field of the result comes back as its default — a
/// replay response is only ever observed when the original response was lost, and the client's job at
/// that point is to confirm the id, not to re-read the derived figures.
/// </summary>
public interface IIdempotentResult
{
    /// <summary>The stored identity for this result (e.g. the created transaction id).</summary>
    Guid IdempotentId { get; }
}

/// <summary>
/// Self-typed <see cref="IIdempotentResult"/> that also supplies the factory used to reconstruct the
/// (id-only) result on a replay hit. Implemented by the concrete result record.
/// </summary>
public interface IIdempotentResult<out TSelf> : IIdempotentResult where TSelf : IIdempotentResult<TSelf>
{
    /// <summary>Rebuilds the result from just its stored id (other fields default). Replay path only.</summary>
    static abstract TSelf FromIdempotentId(Guid id);
}

/// <summary>
/// Reflection helpers (cached) that let the <c>IdempotencyBehavior</c> and cross-module coordinator
/// treat a bare <see cref="Guid"/> result and an <see cref="IIdempotentResult"/> result uniformly
/// without being generically constrained on the inner result type (ADR-0021).
/// </summary>
public static class IdempotentResults
{
    private static readonly ConcurrentDictionary<Type, Func<Guid, object>> ResultFactories = new();
    private static readonly ConcurrentDictionary<Type, Func<object, object?>> ValueGetters = new();

    /// <summary>True when a <c>Result&lt;inner&gt;</c> carries a single-Guid identity we can store/replay.</summary>
    public static bool IsAddressable(Type innerType) =>
        innerType == typeof(Guid) || typeof(IIdempotentResult).IsAssignableFrom(innerType);

    /// <summary>The id to store for a successful result value, or null when it is not addressable.</summary>
    public static Guid? IdOf(object? value) => value switch
    {
        Guid g => g,
        IIdempotentResult r => r.IdempotentId,
        _ => null,
    };

    /// <summary>The <c>Value</c> of a successful <c>Result&lt;T&gt;</c> (boxed), read reflectively.</summary>
    public static object? SuccessValueOf(object result) =>
        ValueGetters.GetOrAdd(result.GetType(), static t =>
        {
            var prop = t.GetProperty(nameof(Result<object>.Value))!;
            return prop.GetValue;
        })(result);

    /// <summary>
    /// Rebuilds a successful <c>Result&lt;inner&gt;</c> (boxed) carrying <paramref name="id"/> for a replay
    /// hit. A bare Guid rebuilds directly; an <see cref="IIdempotentResult"/> rebuilds via its
    /// <c>FromIdempotentId</c> factory (id-only; other fields default).
    /// </summary>
    public static object RebuildResult(Type innerType, Guid id) =>
        ResultFactories.GetOrAdd(innerType, BuildFactory)(id);

    private static Func<Guid, object> BuildFactory(Type innerType)
    {
        var success = typeof(Result)
            .GetMethod(nameof(Result.Success), 1, new[] { Type.MakeGenericMethodParameter(0) })!
            .MakeGenericMethod(innerType);

        if (innerType == typeof(Guid))
        {
            return id => success.Invoke(null, new object[] { id })!;
        }

        var from = innerType.GetMethod(
            "FromIdempotentId", BindingFlags.Public | BindingFlags.Static, new[] { typeof(Guid) })
            ?? throw new InvalidOperationException(
                $"{innerType.Name} must implement IIdempotentResult<{innerType.Name}> (static FromIdempotentId).");

        return id => success.Invoke(null, new[] { from.Invoke(null, new object[] { id }) })!;
    }
}

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
