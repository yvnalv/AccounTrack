using Accountrack.SharedKernel.Domain;

namespace Accountrack.ProcessTracker.Domain;

/// <summary>
/// A single milestone in a document's business lifecycle (GLOSSARY.md: distinct from the Audit Log
/// which is field-level). Append-only; the timeline for a document is all its process events in
/// order. Keyed by (document type, document id) within the company.
/// </summary>
public sealed class ProcessEvent : TenantOwnedEntity, IAggregateRoot
{
    private ProcessEvent() { }

    private ProcessEvent(string documentType, Guid documentId, string milestone, string? note, Guid? actorUserId, DateTime occurredAtUtc)
    {
        DocumentType = documentType.Trim();
        DocumentId = documentId;
        Milestone = milestone.Trim();
        Note = note;
        ActorUserId = actorUserId;
        OccurredAtUtc = occurredAtUtc;
    }

    public string DocumentType { get; private set; } = default!;
    public Guid DocumentId { get; private set; }

    /// <summary>The milestone label, e.g. "Submitted", "Approved", "Posted".</summary>
    public string Milestone { get; private set; } = default!;

    public string? Note { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static ProcessEvent Record(
        string documentType, Guid documentId, string milestone, string? note, Guid? actorUserId, DateTime occurredAtUtc) =>
        new(documentType, documentId, milestone, note, actorUserId, occurredAtUtc);
}
