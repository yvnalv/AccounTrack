using Accountrack.Modules.Contracts.Events;

namespace Accountrack.Application.Abstractions.Integration;

/// <summary>
/// Handles an integration event published by another module (a consumer/subscriber). Resolved and
/// invoked by the outbox dispatcher (ADR-0007); a handler must be idempotent since delivery is
/// at-least-once (the dispatcher de-dups per handler via the inbox — INTEGRATION_EVENTS.md §1).
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct);
}
