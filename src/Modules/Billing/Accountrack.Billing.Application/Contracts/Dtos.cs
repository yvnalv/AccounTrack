using Accountrack.Billing.Domain;

namespace Accountrack.Billing.Application.Contracts;

/// <summary>A plan as shown on the public pricing page. Prices are IDR minor units (§9).</summary>
public sealed record PlanDto(
    Guid Id,
    string Code,
    string Name,
    BillingInterval Interval,
    long BasePriceMinor,
    int IncludedSeats,
    long PerSeatPriceMinor,
    int? MaxCompanies,
    string Currency,
    string FeaturesJson,
    bool IsActive,
    bool IsPublic);

/// <summary>The current tenant's subscription. Null when the tenant has never subscribed.</summary>
public sealed record SubscriptionDto(
    Guid Id,
    Guid PlanId,
    string PlanCode,
    string PlanName,
    SubscriptionStatus Status,
    BillingInterval Interval,
    int ExtraSeats,
    PaymentMode PaymentMode,
    DateOnly? TrialEndsAt,
    DateOnly? CurrentPeriodStart,
    DateOnly? CurrentPeriodEnd,
    bool CancelAtPeriodEnd);
