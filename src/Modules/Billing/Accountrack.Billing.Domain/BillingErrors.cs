using Accountrack.SharedKernel.Results;

namespace Accountrack.Billing.Domain;

/// <summary>Billing failures (SUBSCRIPTION_BILLING.md §7/§9).</summary>
public static class BillingErrors
{
    /// <summary>
    /// The tenant's subscription does not permit business writes (past-due grace, unpaid or expired).
    /// Returned by the enforcement middleware and any command that re-checks entitlements.
    /// </summary>
    public static readonly Error SubscriptionRequired = Error.Forbidden(
        "BILLING.SUBSCRIPTION_REQUIRED",
        "Your subscription does not allow changes right now. Please update your billing to continue.");

    /// <summary>The action would exceed a limit included in the tenant's plan (seats, companies).</summary>
    public static Error PlanLimitReached(string limit, int allowed) => Error.Conflict(
        "BILLING.PLAN_LIMIT_REACHED",
        $"Your plan allows up to {allowed} {limit}. Upgrade your plan to add more.");

    public static readonly Error PlanNotFound =
        Error.NotFound("BILLING.PLAN_NOT_FOUND", "Plan not found.");

    public static readonly Error PlanNotAvailable = Error.Conflict(
        "BILLING.PLAN_NOT_AVAILABLE", "That plan is no longer available.");

    public static readonly Error AlreadySubscribed = Error.Conflict(
        "BILLING.ALREADY_SUBSCRIBED", "This organization already has a subscription.");
}
