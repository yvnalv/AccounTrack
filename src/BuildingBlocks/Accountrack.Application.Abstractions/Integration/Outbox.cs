using Accountrack.Modules.Contracts.Events;

namespace Accountrack.Application.Abstractions.Integration;

/// <summary>
/// Transactional outbox (ADR-0007, INTEGRATION_EVENTS.md §5). A publisher enqueues an integration
/// event into the same unit of work as the change that raised it, so the event is captured atomically
/// and can never be lost. A background dispatcher delivers it to handlers afterwards (at-least-once).
/// </summary>
public interface IOutbox
{
    /// <summary>Stages an event for delivery. It is persisted when the current unit of work is saved.</summary>
    void Enqueue(IIntegrationEvent integrationEvent);
}

/// <summary>A pending outbox row the dispatcher needs to deliver it (Type is assembly-qualified).</summary>
public sealed record OutboxRecord(Guid Id, Guid TenantId, Guid CompanyId, string Type, string Content);

/// <summary>
/// Shared delivery tuning for the outbox so the background dispatcher and the dead-letter admin view
/// agree on the same attempt cap. A message at or beyond <see cref="MaxAttempts"/> is "dead-lettered":
/// the dispatcher stops retrying it until an operator requeues it.
/// </summary>
public static class OutboxDefaults
{
    public const int BatchSize = 50;
    public const int MaxAttempts = 10;
    public const int PollIntervalSeconds = 2;
}

/// <summary>
/// Persistence for the outbox, owned by the publishing module's context (so the enqueue is
/// transactional with the change). The dispatcher reads pending rows and marks their outcome.
/// </summary>
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxRecord>> GetPendingAsync(int max, int maxAttempts, CancellationToken ct);
    Task MarkProcessedAsync(Guid id, CancellationToken ct);
    Task RecordFailureAsync(Guid id, string error, CancellationToken ct);
}

/// <summary>
/// De-duplication store (the "inbox"): records that a given handler has already applied a given event,
/// so an at-least-once redelivery does not double-apply non-idempotent consumers.
/// </summary>
public interface IInboxStore
{
    Task<bool> HasProcessedAsync(string handler, Guid eventId, CancellationToken ct);
    Task MarkProcessedAsync(string handler, Guid eventId, CancellationToken ct);
}

/// <summary>Delivers one pending outbox event to its handlers (run in its own scope per message).</summary>
public interface IOutboxProcessor
{
    Task ProcessAsync(OutboxRecord record, CancellationToken ct);
}

/// <summary>
/// A settable tenant scope for non-HTTP work (the outbox dispatcher): it restores the originating
/// tenant/company so consumers stamp and filter tenant-owned data correctly. The request-time
/// <see cref="Context.ITenantContext"/> prefers HTTP claims and falls back to this when absent.
/// </summary>
public interface IAmbientTenant
{
    Guid TenantId { get; }
    Guid CompanyId { get; }
    bool IsSet { get; }
    void Set(Guid tenantId, Guid companyId);
}
