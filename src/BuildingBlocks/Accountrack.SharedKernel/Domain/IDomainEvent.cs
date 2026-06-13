namespace Accountrack.SharedKernel.Domain;

/// <summary>
/// A domain event raised by an aggregate when something business-meaningful happens.
/// Dispatched in-process; see docs/INTEGRATION_EVENTS.md for the cross-module contract.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTime OccurredAtUtc { get; }
}

/// <summary>Base record for domain events with a generated id and UTC timestamp.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
