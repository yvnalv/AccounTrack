namespace Accountrack.Modules.Contracts.Events;

/// <summary>
/// Marker for an integration event — a fact one module publishes for others to react to
/// (INTEGRATION_EVENTS.md, ADR-0007). Dispatched in-process; a durable outbox is a later hardening.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>Raised when a document is submitted for approval (or auto-approved).</summary>
public sealed record ApprovalSubmitted(
    string DocumentType,
    Guid DocumentId,
    Guid RequestId,
    string Status,
    Guid SubmittedByUserId) : IntegrationEvent;

/// <summary>Raised when an approval request is approved, rejected, or otherwise advanced.</summary>
public sealed record ApprovalDecided(
    string DocumentType,
    Guid DocumentId,
    Guid RequestId,
    string Status,
    Guid SubmittedByUserId,
    Guid DecidedByUserId,
    bool Approved) : IntegrationEvent;
