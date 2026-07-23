using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Application.Features;
using Accountrack.Billing.Domain;
using Accountrack.Modules.Contracts.Billing;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Billing.UnitTests;

/// <summary>
/// Entitlement resolution (SUBSCRIPTION_BILLING.md §7). These assertions decide who gets locked out of
/// their own data, so they are deliberately explicit about every status.
/// </summary>
public class EntitlementResolverTests
{
    private static Plan APlan(int seats = 5, int? maxCompanies = 3, string features = "{\"approvals\":true}") =>
        Plan.Create("BUSINESS-MONTHLY", "Business", BillingInterval.Monthly, 499_000, seats, 49_000,
            maxCompanies, "IDR", features);

    private static Subscription ASubscription(Plan plan, SubscriptionStatus status)
    {
        var today = new DateOnly(2026, 7, 21);
        var subscription = Subscription.StartTrial(
            plan.Id, plan.Interval, PaymentMode.Invoice, today, today.AddDays(14));

        switch (status)
        {
            case SubscriptionStatus.Trialing:
                break;
            case SubscriptionStatus.Active:
                subscription.Activate(today, today.AddMonths(1));
                break;
            case SubscriptionStatus.PastDue:
                subscription.MarkPastDue();
                break;
            case SubscriptionStatus.Unpaid:
                subscription.MarkUnpaid();
                break;
            case SubscriptionStatus.Canceled:
                subscription.CancelAtEndOfPeriod();
                break;
            case SubscriptionStatus.Expired:
                subscription.Expire();
                break;
        }

        // ExtraSeats stays 0 — seats are purchased through the plan-change flow (Slice 3).
        return subscription;
    }

    [Theory]
    [InlineData(SubscriptionStatus.Trialing, TenantAccessLevel.Full)]
    [InlineData(SubscriptionStatus.Active, TenantAccessLevel.Full)]
    // Cancelled keeps access until the period actually ends; the lifecycle then moves it to Expired.
    [InlineData(SubscriptionStatus.Canceled, TenantAccessLevel.Full)]
    // Decided 2026-07-11: grace is read-only — pressure to pay without risking data loss.
    [InlineData(SubscriptionStatus.PastDue, TenantAccessLevel.ReadOnly)]
    [InlineData(SubscriptionStatus.Unpaid, TenantAccessLevel.Locked)]
    [InlineData(SubscriptionStatus.Expired, TenantAccessLevel.Locked)]
    public void Access_level_follows_subscription_status(SubscriptionStatus status, TenantAccessLevel expected) =>
        EntitlementResolver.AccessLevelFor(status).Should().Be(expected);

    [Theory]
    [InlineData(SubscriptionStatus.Trialing, true)]
    [InlineData(SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.PastDue, false)]
    [InlineData(SubscriptionStatus.Unpaid, false)]
    [InlineData(SubscriptionStatus.Expired, false)]
    public void Writes_are_allowed_only_at_full_access(SubscriptionStatus status, bool canWrite)
    {
        var plan = APlan();
        EntitlementResolver.Build(ASubscription(plan, status), plan).CanWrite.Should().Be(canWrite);
    }

    /// <summary>
    /// The safety property that makes the guard deployable: every tenant that predates billing has no
    /// subscription row, and must keep working. Getting this wrong locks out every existing customer.
    /// </summary>
    [Fact]
    public async Task A_tenant_with_no_subscription_is_unrestricted()
    {
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        var plans = Substitute.For<IPlanRepository>();
        subscriptions.GetForCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns((Subscription?)null);

        var result = await new EntitlementResolver(subscriptions, plans)
            .GetForCurrentTenantAsync(CancellationToken.None);

        result.HasSubscription.Should().BeFalse();
        result.AccessLevel.Should().Be(TenantAccessLevel.Full);
        result.CanWrite.Should().BeTrue();
        result.MaxUsers.Should().BeNull();
        result.MaxCompanies.Should().BeNull();
        await plans.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Entitlements_are_projected_from_the_subscription_and_its_plan()
    {
        var plan = APlan(seats: 8, maxCompanies: 3);
        var subscription = ASubscription(plan, SubscriptionStatus.Active);

        var subscriptions = Substitute.For<ISubscriptionRepository>();
        var plans = Substitute.For<IPlanRepository>();
        subscriptions.GetForCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns(subscription);
        plans.GetByIdAsync(subscription.PlanId, Arg.Any<CancellationToken>()).Returns(plan);

        var result = await new EntitlementResolver(subscriptions, plans)
            .GetForCurrentTenantAsync(CancellationToken.None);

        result.HasSubscription.Should().BeTrue();
        result.PlanCode.Should().Be("BUSINESS-MONTHLY");
        result.Status.Should().Be(nameof(SubscriptionStatus.Active));
        result.MaxUsers.Should().Be(8);          // included seats + purchased extras
        result.MaxCompanies.Should().Be(3);
        result.HasFeature("approvals").Should().BeTrue();
        result.HasFeature("APPROVALS").Should().BeTrue("feature lookup is case-insensitive");
        result.HasFeature("nonexistent").Should().BeFalse("unknown flags default to off");
    }

    [Fact]
    public void An_unlimited_plan_reports_no_company_cap()
    {
        var plan = APlan(maxCompanies: null);
        EntitlementResolver.Build(ASubscription(plan, SubscriptionStatus.Active), plan)
            .MaxCompanies.Should().BeNull();
    }

    /// <summary>A malformed or absent feature blob must not break resolution — it degrades to no features.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("{}")]
    public void Malformed_plan_features_degrade_to_none(string features)
    {
        var plan = APlan(features: features);
        var result = EntitlementResolver.Build(ASubscription(plan, SubscriptionStatus.Active), plan);

        result.HasFeature("approvals").Should().BeFalse();
        result.CanWrite.Should().BeTrue("a bad feature blob must never revoke access");
    }
}
