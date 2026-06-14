using Accountrack.Application.Abstractions.Context;
using Accountrack.Modules.Contracts.Events;
using Accountrack.ProcessTracker.Application;
using Accountrack.ProcessTracker.Application.Abstractions;
using Accountrack.ProcessTracker.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.ProcessTracker.UnitTests;

public class ApprovalProcessConsumerTests
{
    private readonly IProcessTimelineRepository _timeline = Substitute.For<IProcessTimelineRepository>();
    private readonly IProcessTrackerUnitOfWork _uow = Substitute.For<IProcessTrackerUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Guid _doc = Guid.NewGuid();

    public ApprovalProcessConsumerTests() =>
        _clock.UtcNow.Returns(new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc));

    private ApprovalProcessConsumer Consumer() => new(_timeline, _uow, _clock);

    [Theory]
    [InlineData("Pending", "Submitted for approval")]
    [InlineData("AutoApproved", "Auto-approved")]
    public async Task Submitted_records_the_right_milestone(string status, string expected)
    {
        ProcessEvent? captured = null;
        _timeline.Add(Arg.Do<ProcessEvent>(e => captured = e));

        await Consumer().HandleAsync(
            new ApprovalSubmitted("PurchaseOrder", _doc, Guid.NewGuid(), status, Guid.NewGuid()), CancellationToken.None);

        captured!.Milestone.Should().Be(expected);
        captured.DocumentType.Should().Be("PurchaseOrder");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true, "Approved", "Approved")]
    [InlineData(false, "Rejected", "Rejected")]
    [InlineData(true, "Pending", "Approval advanced")]
    public async Task Decided_records_the_right_milestone(bool approved, string status, string expected)
    {
        ProcessEvent? captured = null;
        _timeline.Add(Arg.Do<ProcessEvent>(e => captured = e));

        await Consumer().HandleAsync(
            new ApprovalDecided("PurchaseOrder", _doc, Guid.NewGuid(), status, Guid.NewGuid(), Guid.NewGuid(), approved),
            CancellationToken.None);

        captured!.Milestone.Should().Be(expected);
    }
}
