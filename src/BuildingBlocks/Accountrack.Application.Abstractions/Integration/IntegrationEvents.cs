using Accountrack.Modules.Contracts.Events;

namespace Accountrack.Application.Abstractions.Integration;

/// <summary>Handles an integration event published by another module (a consumer/subscriber).</summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct);
}

/// <summary>
/// Publishes an integration event to all registered handlers. Implemented in Infrastructure as a
/// best-effort in-process dispatch — a failing handler is logged and does not fail the publisher
/// (eventual semantics; a durable outbox is a later hardening — INTEGRATION_EVENTS.md §5).
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct)
        where TEvent : IIntegrationEvent;
}
