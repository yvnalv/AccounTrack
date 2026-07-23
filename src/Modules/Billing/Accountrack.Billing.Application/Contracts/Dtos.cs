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

/// <summary>Result of starting checkout: where to pay and for how much.</summary>
public sealed record CheckoutDto(Guid BillingInvoiceId, string PayUrl, long AmountMinor, string Currency);

/// <summary>A billing invoice Accountrack issued to the tenant, for the billing history (§9).</summary>
public sealed record BillingInvoiceDto(
    Guid Id,
    string Number,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    long TotalMinor,
    string Currency,
    BillingInvoiceStatus Status,
    DateOnly DueDate,
    DateTime? PaidAt);

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
