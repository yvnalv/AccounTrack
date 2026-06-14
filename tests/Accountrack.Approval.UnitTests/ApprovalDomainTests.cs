using Accountrack.Approval.Domain;
using FluentAssertions;
using Xunit;

namespace Accountrack.Approval.UnitTests;

public class ApprovalDomainTests
{
    private static readonly DateTime Now = new(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);

    private static ApprovalDefinition TwoLevelDefinition(string roleL1 = "Supervisor", string roleL2 = "Director")
    {
        var def = new ApprovalDefinition("PurchaseOrder", "PO over 50m");
        def.AddCondition("Total", ConditionOperator.GreaterThan, 50_000_000m);
        def.AddStep(1, ApproverType.Role, roleL1);
        def.AddStep(2, ApproverType.Role, roleL2);
        return def;
    }

    [Fact]
    public void Definition_matches_only_when_all_conditions_pass()
    {
        var def = TwoLevelDefinition();
        def.Matches(new Dictionary<string, decimal> { ["Total"] = 60_000_000m }).Should().BeTrue();
        def.Matches(new Dictionary<string, decimal> { ["Total"] = 40_000_000m }).Should().BeFalse();
        def.Matches(new Dictionary<string, decimal>()).Should().BeFalse(); // missing attribute
    }

    [Fact]
    public void Definition_with_no_conditions_always_matches() =>
        new ApprovalDefinition("Expense", "always").Matches(new Dictionary<string, decimal>())
            .Should().BeTrue();

    [Fact]
    public void Multi_level_request_advances_then_approves()
    {
        var submitter = Guid.NewGuid();
        var sup = Guid.NewGuid();
        var dir = Guid.NewGuid();
        var req = ApprovalRequest.CreatePending(TwoLevelDefinition(), Guid.NewGuid(), submitter);

        req.CurrentLevel.Should().Be(1);
        req.MaxLevel.Should().Be(2);

        req.IsEligible(sup, new[] { "Supervisor" }).Should().BeTrue();
        req.IsEligible(dir, new[] { "Director" }).Should().BeFalse(); // not their level yet

        req.Approve(sup, "ok", Now);
        req.Status.Should().Be(ApprovalRequestStatus.Pending);
        req.CurrentLevel.Should().Be(2);

        req.IsEligible(dir, new[] { "Director" }).Should().BeTrue();
        req.Approve(dir, "ok", Now);
        req.Status.Should().Be(ApprovalRequestStatus.Approved);
    }

    [Fact]
    public void Rejection_at_any_level_rejects_the_request()
    {
        var req = ApprovalRequest.CreatePending(TwoLevelDefinition(), Guid.NewGuid(), Guid.NewGuid());
        req.Reject(Guid.NewGuid(), "no", Now);
        req.Status.Should().Be(ApprovalRequestStatus.Rejected);
    }

    [Fact]
    public void A_decided_request_cannot_be_acted_on_again()
    {
        var req = ApprovalRequest.CreatePending(TwoLevelDefinition(), Guid.NewGuid(), Guid.NewGuid());
        req.Reject(Guid.NewGuid(), null, Now);
        FluentActions.Invoking(() => req.Approve(Guid.NewGuid(), null, Now))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void User_type_step_is_eligible_only_for_that_user()
    {
        var def = new ApprovalDefinition("Expense", "single");
        var approver = Guid.NewGuid();
        def.AddStep(1, ApproverType.User, approver.ToString());
        var req = ApprovalRequest.CreatePending(def, Guid.NewGuid(), Guid.NewGuid());

        req.IsEligible(approver, Array.Empty<string>()).Should().BeTrue();
        req.IsEligible(Guid.NewGuid(), Array.Empty<string>()).Should().BeFalse();
    }

    [Fact]
    public void Auto_approved_request_has_no_steps_and_completed_status()
    {
        var req = ApprovalRequest.CreateAutoApproved("Expense", Guid.NewGuid(), Guid.NewGuid());
        req.Status.Should().Be(ApprovalRequestStatus.AutoApproved);
        req.IsPending.Should().BeFalse();
        req.Steps.Should().BeEmpty();
    }
}
