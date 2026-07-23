using Accountrack.Application.Abstractions.Context;
using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Application.Features;
using Accountrack.Billing.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Billing.UnitTests;

/// <summary>Starting a free trial (SUBSCRIPTION_BILLING.md §6.2).</summary>
public class StartTrialTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 9, 0, 0, DateTimeKind.Utc);

    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly IPlanRepository _plans = Substitute.For<IPlanRepository>();
    private readonly IBillingUnitOfWork _uow = Substitute.For<IBillingUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public StartTrialTests() => _clock.UtcNow.Returns(Now);

    private StartTrialHandler Handler() => new(_subscriptions, _plans, _uow, _clock);

    private static Plan APlan(bool active = true)
    {
        var plan = Plan.Create("STARTER-MONTHLY", "Starter", BillingInterval.Monthly, 149_000, 2, 59_000,
            1, "IDR", "{}");
        if (!active)
        {
            plan.Deactivate();
        }

        return plan;
    }

    [Fact]
    public async Task Starts_a_14_day_trial_on_the_requested_plan()
    {
        var plan = APlan();
        _subscriptions.GetForCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns((Subscription?)null);
        _plans.GetByCodeAsync("STARTER-MONTHLY", Arg.Any<CancellationToken>()).Returns(plan);

        Subscription? added = null;
        _subscriptions.When(s => s.Add(Arg.Any<Subscription>())).Do(ci => added = ci.Arg<Subscription>());

        var result = await Handler().Handle(new StartTrialCommand("starter-monthly"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.Status.Should().Be(SubscriptionStatus.Trialing);
        added.PlanId.Should().Be(plan.Id);
        added.TrialEndsAt.Should().Be(new DateOnly(2026, 8, 4)); // 21 Jul + 14 days
        added.ExtraSeats.Should().Be(0);
        added.PaymentMode.Should().Be(PaymentMode.Invoice);
        // A trial grants full access immediately.
        added.GrantsFullAccess.Should().BeTrue();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_second_subscription_for_the_same_tenant()
    {
        var plan = APlan();
        _subscriptions.GetForCurrentTenantAsync(Arg.Any<CancellationToken>())
            .Returns(Subscription.StartTrial(plan.Id, plan.Interval, PaymentMode.Invoice,
                new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15)));

        var result = await Handler().Handle(new StartTrialCommand("STARTER-MONTHLY"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BILLING.ALREADY_SUBSCRIBED");
        _subscriptions.DidNotReceive().Add(Arg.Any<Subscription>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_an_unknown_plan()
    {
        _subscriptions.GetForCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns((Subscription?)null);
        _plans.GetByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Plan?)null);

        var result = await Handler().Handle(new StartTrialCommand("NOPE"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BILLING.PLAN_NOT_FOUND");
        _subscriptions.DidNotReceive().Add(Arg.Any<Subscription>());
    }

    [Fact]
    public async Task Rejects_a_retired_plan()
    {
        _subscriptions.GetForCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns((Subscription?)null);
        _plans.GetByCodeAsync("STARTER-MONTHLY", Arg.Any<CancellationToken>()).Returns(APlan(active: false));

        var result = await Handler().Handle(new StartTrialCommand("STARTER-MONTHLY"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BILLING.PLAN_NOT_AVAILABLE");
        _subscriptions.DidNotReceive().Add(Arg.Any<Subscription>());
    }
}
