namespace Accountrack.Billing.Domain;

/// <summary>Billing cadence for a plan/subscription (SUBSCRIPTION_BILLING.md §2).</summary>
public enum BillingInterval
{
    Monthly = 0,
    Annual = 1,
}

/// <summary>
/// Subscription lifecycle (SUBSCRIPTION_BILLING.md §6.1). Decided 2026-07-11: access is read-only
/// during <see cref="PastDue"/> grace and hard-locked at <see cref="Expired"/>/<see cref="Unpaid"/>.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>Full access, no charge, until the trial ends.</summary>
    Trialing = 0,
    /// <summary>Paid; the current period end is in the future.</summary>
    Active = 1,
    /// <summary>A charge/invoice failed or lapsed; grace period with read-only access + banner.</summary>
    PastDue = 2,
    /// <summary>User cancelled; access until the current period end, then expires.</summary>
    Canceled = 3,
    /// <summary>Grace exhausted after a failed payment — locked out (terminal until re-subscribe).</summary>
    Unpaid = 4,
    /// <summary>Period ended after cancellation — locked out; data retained for the retention window.</summary>
    Expired = 5,
}

/// <summary>How each cycle is collected (SUBSCRIPTION_BILLING.md §4).</summary>
public enum PaymentMode
{
    /// <summary>Tokenized card / linked e-wallet charged automatically each cycle.</summary>
    AutoCharge = 0,
    /// <summary>A hosted invoice (VA/QRIS/e-wallet/card) issued each cycle; the customer pays it.</summary>
    Invoice = 1,
    /// <summary>Offline bank transfer, admin-confirmed activation.</summary>
    Manual = 2,
}

/// <summary>Status of a billing invoice Accountrack issues to a tenant (SUBSCRIPTION_BILLING.md §9).</summary>
public enum BillingInvoiceStatus
{
    Draft = 0,
    /// <summary>Issued and awaiting payment.</summary>
    Open = 1,
    Paid = 2,
    /// <summary>Cancelled before payment.</summary>
    Void = 3,
    /// <summary>Written off as uncollectible.</summary>
    Uncollectible = 4,
}
