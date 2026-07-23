using Accountrack.SharedKernel.Domain;

namespace Accountrack.Billing.Domain;

/// <summary>
/// A tenant's subscription to a <see cref="Plan"/> (SUBSCRIPTION_BILLING.md §6/§9, ADR-0039). Tenant-scoped
/// (one subscription owner = one Tenant; billing is not per-company), so it carries a tenant filter but no
/// company. This is our commercial record — it never posts into the tenant's GL. Lifecycle transitions are
/// driven by the gateway webhook (the source of truth for "paid", §5); this slice models the state, later
/// slices drive it.
/// </summary>
public sealed class Subscription : TenantScopedEntity, IAggregateRoot
{
    private Subscription() { }

    private Subscription(Guid planId, SubscriptionStatus status, BillingInterval interval, int extraSeats,
        PaymentMode paymentMode, DateOnly? trialEndsAt, DateOnly? currentPeriodStart, DateOnly? currentPeriodEnd)
    {
        PlanId = planId;
        Status = status;
        Interval = interval;
        ExtraSeats = extraSeats;
        PaymentMode = paymentMode;
        TrialEndsAt = trialEndsAt;
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
    }

    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public BillingInterval Interval { get; private set; }

    /// <summary>Purchased seats beyond the plan's included seats.</summary>
    public int ExtraSeats { get; private set; }

    public PaymentMode PaymentMode { get; private set; }

    public DateOnly? TrialEndsAt { get; private set; }
    public DateOnly? CurrentPeriodStart { get; private set; }
    public DateOnly? CurrentPeriodEnd { get; private set; }

    /// <summary>When true the subscription ends at <see cref="CurrentPeriodEnd"/> instead of renewing.</summary>
    public bool CancelAtPeriodEnd { get; private set; }

    // Gateway linkage (populated once the Xendit adapter lands, Slice 3). Ids only — no card data (§8).
    public string? GatewayCustomerId { get; private set; }
    public string? GatewaySubscriptionId { get; private set; }

    /// <summary>Business writes are allowed under trial or active, and read-only during past-due grace (§7).</summary>
    public bool GrantsFullAccess => Status is SubscriptionStatus.Trialing or SubscriptionStatus.Active;

    public bool IsReadOnlyGrace => Status == SubscriptionStatus.PastDue;

    /// <summary>Starts a free trial on a plan (no charge until <paramref name="trialEndsAt"/>; §6.2).</summary>
    public static Subscription StartTrial(Guid planId, BillingInterval interval, PaymentMode paymentMode,
        DateOnly today, DateOnly trialEndsAt) =>
        new(planId, SubscriptionStatus.Trialing, interval, extraSeats: 0, paymentMode, trialEndsAt,
            currentPeriodStart: today, currentPeriodEnd: trialEndsAt);

    /// <summary>
    /// Switches to a different plan. Immediate for this phase; proration and
    /// downgrade-at-period-end (§6.3) are a later phase. The tenant then pays for the new plan's period.
    /// </summary>
    public void ChangePlan(Guid planId, BillingInterval interval)
    {
        PlanId = planId;
        Interval = interval;
    }

    /// <summary>Marks the subscription active for a paid period (driven by the payment webhook, §5).</summary>
    public void Activate(DateOnly periodStart, DateOnly periodEnd)
    {
        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        CancelAtPeriodEnd = false;
    }

    /// <summary>A charge/invoice failed — enter the read-only grace period (§6.4).</summary>
    public void MarkPastDue() => Status = SubscriptionStatus.PastDue;

    /// <summary>Grace exhausted — lock out until re-subscribe.</summary>
    public void MarkUnpaid() => Status = SubscriptionStatus.Unpaid;

    /// <summary>User cancels; access continues until the period end (§6.3).</summary>
    public void CancelAtEndOfPeriod()
    {
        Status = SubscriptionStatus.Canceled;
        CancelAtPeriodEnd = true;
    }

    /// <summary>The cancelled period has ended — lock out (data retained for the retention window).</summary>
    public void Expire() => Status = SubscriptionStatus.Expired;

    public void LinkGateway(string customerId, string? subscriptionId)
    {
        GatewayCustomerId = customerId;
        GatewaySubscriptionId = subscriptionId;
    }
}
