using Accountrack.Application.Abstractions.Integration;
using Accountrack.Modules.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Accountrack.Infrastructure.Common.Integration;

/// <summary>
/// In-process integration-event publisher (ADR-0007). Resolves all registered handlers for the
/// event type and invokes them best-effort: a handler failure is logged but does not fail the
/// publisher, since these are eventual side effects (a durable outbox is a later hardening).
/// </summary>
public sealed class IntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly IServiceProvider _services;
    private readonly ILogger<IntegrationEventPublisher> _logger;

    public IntegrationEventPublisher(IServiceProvider services, ILogger<IntegrationEventPublisher> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct)
        where TEvent : IIntegrationEvent
    {
        var handlers = _services.GetServices<IIntegrationEventHandler<TEvent>>();

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(integrationEvent, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Integration event handler {Handler} failed for {Event} ({EventId})",
                    handler.GetType().Name, typeof(TEvent).Name, integrationEvent.EventId);
            }
        }
    }
}
