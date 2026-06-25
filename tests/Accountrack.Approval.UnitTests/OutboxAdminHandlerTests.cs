using Accountrack.Application.Abstractions.Integration;
using Accountrack.Approval.Application.Abstractions;
using Accountrack.Approval.Application.Features;
using Accountrack.Approval.Domain;
using Accountrack.Modules.Contracts.Events;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Approval.UnitTests;

/// <summary>
/// Dead-letter triage handlers: the list query reduces the stored assembly-qualified type to a bare
/// event name, and the retry command requeues a message (or reports not-found).
/// </summary>
public class OutboxAdminHandlerTests
{
    private readonly IOutboxAdminRepository _outbox = Substitute.For<IOutboxAdminRepository>();

    [Fact]
    public async Task List_query_passes_the_shared_attempt_cap_and_maps_friendly_event_name()
    {
        var occurred = new DateTime(2026, 6, 24, 9, 0, 0, DateTimeKind.Utc);
        _outbox.ListDeadLetteredAsync(OutboxDefaults.MaxAttempts, Arg.Any<CancellationToken>())
            .Returns(new List<DeadLetterMessage>
            {
                new(Guid.NewGuid(), typeof(ApprovalDecided).AssemblyQualifiedName!, occurred, 10, "boom"),
            });

        var result = await new GetDeadLetteredEventsHandler(_outbox)
            .Handle(new GetDeadLetteredEventsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        var dto = result.Value[0];
        dto.EventType.Should().Be("ApprovalDecided"); // not the assembly-qualified name
        dto.Attempts.Should().Be(10);
        dto.Error.Should().Be("boom");
        dto.OccurredOnUtc.Should().Be(occurred);
        await _outbox.Received(1).ListDeadLetteredAsync(OutboxDefaults.MaxAttempts, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retry_succeeds_when_the_message_is_requeued()
    {
        var id = Guid.NewGuid();
        _outbox.RequeueAsync(id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await new RetryDeadLetteredEventHandler(_outbox)
            .Handle(new RetryDeadLetteredEventCommand(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _outbox.Received(1).RequeueAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Retry_returns_not_found_when_the_message_is_missing()
    {
        _outbox.RequeueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await new RetryDeadLetteredEventHandler(_outbox)
            .Handle(new RetryDeadLetteredEventCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApprovalErrors.OutboxMessageNotFound);
    }
}
