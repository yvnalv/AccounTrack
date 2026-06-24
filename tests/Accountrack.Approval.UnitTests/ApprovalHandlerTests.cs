using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Integration;
using Accountrack.Approval.Application.Abstractions;
using Accountrack.Approval.Application.Features;
using Accountrack.Approval.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Approval.UnitTests;

public class ApprovalHandlerTests
{
    private static readonly DateTime Now = new(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);

    private readonly IApprovalDefinitionRepository _defs = Substitute.For<IApprovalDefinitionRepository>();
    private readonly IApprovalRequestRepository _reqs = Substitute.For<IApprovalRequestRepository>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IApprovalUnitOfWork _uow = Substitute.For<IApprovalUnitOfWork>();
    private readonly IOutbox _outbox = Substitute.For<IOutbox>();

    public ApprovalHandlerTests() => _clock.UtcNow.Returns(Now);

    private static ApprovalDefinition PoOver50m()
    {
        var def = new ApprovalDefinition("PurchaseOrder", "PO over 50m");
        def.AddCondition("Total", ConditionOperator.GreaterThan, 50_000_000m);
        def.AddStep(1, ApproverType.Role, "Director");
        return def;
    }

    private SubmitForApprovalHandler SubmitHandler() => new(_defs, _reqs, _user, _uow, _outbox);
    private DecideApprovalHandler DecideHandler() => new(_reqs, _user, _clock, _uow, _outbox);

    [Fact]
    public async Task Submit_creates_a_pending_request_when_a_definition_matches()
    {
        _user.UserId.Returns(Guid.NewGuid());
        _reqs.ExistsForDocumentAsync("PurchaseOrder", Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _defs.GetActiveForDocumentTypeAsync("PurchaseOrder", Arg.Any<CancellationToken>())
            .Returns(new List<ApprovalDefinition> { PoOver50m() });

        var result = await SubmitHandler().Handle(
            new SubmitForApprovalCommand("PurchaseOrder", Guid.NewGuid(),
                new Dictionary<string, decimal> { ["Total"] = 60_000_000m }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Pending");
        _reqs.Received(1).Add(Arg.Any<ApprovalRequest>());
    }

    [Fact]
    public async Task Submit_auto_approves_when_no_definition_matches()
    {
        _user.UserId.Returns(Guid.NewGuid());
        _reqs.ExistsForDocumentAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        _defs.GetActiveForDocumentTypeAsync("PurchaseOrder", Arg.Any<CancellationToken>())
            .Returns(new List<ApprovalDefinition> { PoOver50m() });

        var result = await SubmitHandler().Handle(
            new SubmitForApprovalCommand("PurchaseOrder", Guid.NewGuid(),
                new Dictionary<string, decimal> { ["Total"] = 10_000_000m }), CancellationToken.None);

        result.Value.Status.Should().Be("AutoApproved");
    }

    [Fact]
    public async Task Submitter_cannot_approve_their_own_request()
    {
        var submitter = Guid.NewGuid();
        var request = ApprovalRequest.CreatePending(PoOver50m(), Guid.NewGuid(), submitter);
        _reqs.GetAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _user.UserId.Returns(submitter);
        _user.Roles.Returns(new[] { "Director" });

        var result = await DecideHandler().Handle(
            new DecideApprovalCommand(request.Id, true, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApprovalErrors.SelfApproval);
    }

    [Fact]
    public async Task Ineligible_user_cannot_approve()
    {
        var request = ApprovalRequest.CreatePending(PoOver50m(), Guid.NewGuid(), Guid.NewGuid());
        _reqs.GetAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _user.UserId.Returns(Guid.NewGuid());
        _user.Roles.Returns(new[] { "Sales" }); // not Director

        var result = await DecideHandler().Handle(
            new DecideApprovalCommand(request.Id, true, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ApprovalErrors.NotEligible);
    }

    [Fact]
    public async Task Eligible_non_submitter_approves_successfully()
    {
        var request = ApprovalRequest.CreatePending(PoOver50m(), Guid.NewGuid(), Guid.NewGuid());
        _reqs.GetAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _user.UserId.Returns(Guid.NewGuid());
        _user.Roles.Returns(new[] { "Director" });

        var result = await DecideHandler().Handle(
            new DecideApprovalCommand(request.Id, true, "approved"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Approved");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        // The decision event is staged in the outbox (committed with the request, delivered async).
        _outbox.Received(1).Enqueue(Arg.Is<Accountrack.Modules.Contracts.Events.ApprovalDecided>(
            e => e.RequestId == request.Id && e.Approved));
    }
}
