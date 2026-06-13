namespace Accountrack.Infrastructure.Common.Outbox;

/// <summary>
/// A persisted integration event awaiting dispatch (transactional outbox — ADR-0007).
/// Written in the same transaction as the source change; a background dispatcher publishes it
/// and stamps <see cref="ProcessedOnUtc"/>. See docs/INTEGRATION_EVENTS.md.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Assembly-qualified type name of the integration event.</summary>
    public string Type { get; set; } = default!;

    /// <summary>Serialized event payload (JSON).</summary>
    public string Content { get; set; } = default!;

    public DateTime OccurredOnUtc { get; set; }

    public DateTime? ProcessedOnUtc { get; set; }

    public int Attempts { get; set; }

    public string? Error { get; set; }
}
