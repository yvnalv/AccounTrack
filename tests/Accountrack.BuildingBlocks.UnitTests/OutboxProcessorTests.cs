using System.Text.Json;
using Accountrack.Application.Abstractions.Integration;
using Accountrack.Infrastructure.Common.Outbox;
using Accountrack.Modules.Contracts.Events;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Accountrack.BuildingBlocks.UnitTests;

/// <summary>
/// The dispatcher's per-message processor: delivers an event to every registered handler exactly once
/// (de-duplicated via the inbox), restores the originating tenant, and leaves the message pending on a
/// handler failure so the next pass retries without re-applying the handlers that already succeeded.
/// </summary>
public class OutboxProcessorTests
{
    private readonly IOutboxStore _store = Substitute.For<IOutboxStore>();
    private readonly IInboxStore _inbox = Substitute.For<IInboxStore>();
    private readonly IAmbientTenant _ambient = Substitute.For<IAmbientTenant>();

    private static OutboxRecord RecordFor(ApprovalDecided evt) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        typeof(ApprovalDecided).AssemblyQualifiedName!,
        JsonSerializer.Serialize(evt, typeof(ApprovalDecided)));

    private static ApprovalDecided SampleEvent() => new(
        "ExpenseVoucher", Guid.NewGuid(), Guid.NewGuid(), "Approved",
        Guid.NewGuid(), Guid.NewGuid(), true);

    private OutboxProcessor ProcessorWith(params object[] handlers)
    {
        var services = new ServiceCollection();
        foreach (var h in handlers)
        {
            services.AddSingleton(typeof(IIntegrationEventHandler<ApprovalDecided>), h);
        }
        var provider = services.BuildServiceProvider();
        return new OutboxProcessor(provider, _store, _inbox, _ambient,
            NullLogger<OutboxProcessor>.Instance);
    }

    private sealed class RecordingHandler : IIntegrationEventHandler<ApprovalDecided>
    {
        public int Calls { get; private set; }
        public Task HandleAsync(ApprovalDecided integrationEvent, CancellationToken ct)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IIntegrationEventHandler<ApprovalDecided>
    {
        public Task HandleAsync(ApprovalDecided integrationEvent, CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task Delivers_to_handler_restores_tenant_and_marks_processed()
    {
        var handler = new RecordingHandler();
        var evt = SampleEvent();
        var record = RecordFor(evt);
        _inbox.HasProcessedAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await ProcessorWith(handler).ProcessAsync(record, CancellationToken.None);

        handler.Calls.Should().Be(1);
        _ambient.Received(1).Set(record.TenantId, record.CompanyId);
        await _inbox.Received(1).MarkProcessedAsync(
            typeof(RecordingHandler).FullName!, evt.EventId, Arg.Any<CancellationToken>());
        await _store.Received(1).MarkProcessedAsync(record.Id, Arg.Any<CancellationToken>());
        await _store.DidNotReceive().RecordFailureAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_handler_already_recorded_in_inbox()
    {
        var handler = new RecordingHandler();
        var record = RecordFor(SampleEvent());
        _inbox.HasProcessedAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true); // already applied on an earlier delivery

        await ProcessorWith(handler).ProcessAsync(record, CancellationToken.None);

        handler.Calls.Should().Be(0);
        await _inbox.DidNotReceive().MarkProcessedAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _store.Received(1).MarkProcessedAsync(record.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handler_failure_records_failure_and_does_not_mark_processed()
    {
        var record = RecordFor(SampleEvent());
        _inbox.HasProcessedAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await ProcessorWith(new ThrowingHandler()).ProcessAsync(record, CancellationToken.None);

        await _store.Received(1).RecordFailureAsync(
            record.Id, Arg.Is<string>(s => s.Contains("boom")), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().MarkProcessedAsync(record.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_event_type_records_failure()
    {
        var record = new OutboxRecord(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Made.Up.Type, Nonexistent.Assembly", "{}");

        await ProcessorWith().ProcessAsync(record, CancellationToken.None);

        await _store.Received(1).RecordFailureAsync(
            record.Id, Arg.Is<string>(s => s.Contains("Unknown event type")), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().MarkProcessedAsync(record.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Multiple_handlers_each_applied_once()
    {
        var h1 = new RecordingHandler();
        var h2 = new RecordingHandler();
        var record = RecordFor(SampleEvent());
        _inbox.HasProcessedAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await ProcessorWith(h1, h2).ProcessAsync(record, CancellationToken.None);

        h1.Calls.Should().Be(1);
        h2.Calls.Should().Be(1);
        await _store.Received(1).MarkProcessedAsync(record.Id, Arg.Any<CancellationToken>());
    }
}
