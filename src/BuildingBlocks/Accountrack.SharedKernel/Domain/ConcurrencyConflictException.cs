namespace Accountrack.SharedKernel.Domain;

/// <summary>
/// Thrown when an optimistic-concurrency check fails on save — the row was changed (or removed) by
/// another transaction since it was loaded (ADR-0021). Infrastructure translates EF Core's
/// <c>DbUpdateConcurrencyException</c> into this provider-agnostic type so the web layer can map it to
/// a 409 Conflict without taking a dependency on EF.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
