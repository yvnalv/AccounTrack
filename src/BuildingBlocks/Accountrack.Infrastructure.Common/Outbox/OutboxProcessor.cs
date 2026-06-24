using System.Reflection;
using System.Text.Json;
using Accountrack.Application.Abstractions.Integration;
using Accountrack.Modules.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Accountrack.Infrastructure.Common.Outbox;

/// <summary>
/// Delivers one outbox event to every registered handler, in its own scope (so each message runs with
/// fresh module contexts and its own tenant). De-duplicates per (handler, eventId) via the inbox so an
/// at-least-once redelivery never double-applies a consumer; any handler failure leaves the message
/// pending for retry (the already-applied handlers are skipped next time).
/// </summary>
public sealed class OutboxProcessor : IOutboxProcessor
{
    private readonly IServiceProvider _services;
    private readonly IOutboxStore _store;
    private readonly IInboxStore _inbox;
    private readonly IAmbientTenant _ambient;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceProvider services, IOutboxStore store, IInboxStore inbox,
        IAmbientTenant ambient, ILogger<OutboxProcessor> logger)
    {
        _services = services;
        _store = store;
        _inbox = inbox;
        _ambient = ambient;
        _logger = logger;
    }

    public async Task ProcessAsync(OutboxRecord record, CancellationToken ct)
    {
        try
        {
            var eventType = Type.GetType(record.Type);
            if (eventType is null)
            {
                await _store.RecordFailureAsync(record.Id, $"Unknown event type '{record.Type}'.", ct);
                return;
            }

            if (JsonSerializer.Deserialize(record.Content, eventType) is not IIntegrationEvent evt)
            {
                await _store.RecordFailureAsync(record.Id, "Event payload did not deserialize.", ct);
                return;
            }

            // Restore the originating tenant so handlers stamp/filter tenant-owned data correctly.
            _ambient.Set(record.TenantId, record.CompanyId);

            var handlerInterface = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            var handle = handlerInterface.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;

            foreach (var handler in _services.GetServices(handlerInterface))
            {
                if (handler is null)
                {
                    continue;
                }

                var handlerName = handler.GetType().FullName!;
                if (await _inbox.HasProcessedAsync(handlerName, evt.EventId, ct))
                {
                    continue; // already applied on an earlier delivery
                }

                try
                {
                    await (Task)handle.Invoke(handler, new object[] { evt, ct })!;
                }
                catch (TargetInvocationException tie) when (tie.InnerException is not null)
                {
                    // Surface the handler's real failure, not the reflection wrapper.
                    throw tie.InnerException;
                }

                await _inbox.MarkProcessedAsync(handlerName, evt.EventId, ct);
            }

            await _store.MarkProcessedAsync(record.Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Outbox delivery failed for message {MessageId}; will retry.", record.Id);
            await _store.RecordFailureAsync(record.Id, ex.Message, ct);
        }
    }
}
