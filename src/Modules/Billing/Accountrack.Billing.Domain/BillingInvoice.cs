using Accountrack.SharedKernel.Domain;

namespace Accountrack.Billing.Domain;

/// <summary>
/// An invoice Accountrack issues to a tenant for a subscription period (SUBSCRIPTION_BILLING.md §9, ADR-0039).
/// This is the <b>Billing Invoice</b> — Accountrack → a tenant — never to be conflated with the ERP's own
/// Sales Invoice (a tenant → their customer). Tenant-scoped; our commercial ledger, not the tenant's GL.
/// Money is integer minor units + currency. <see cref="TaxMinor"/> is retained but zero while the operating
/// entity is not PKP (decided 2026-07-11, §10); it flips on without a migration once PKP.
/// </summary>
public sealed class BillingInvoice : TenantScopedEntity, IAggregateRoot
{
    private BillingInvoice() { }

    private BillingInvoice(Guid subscriptionId, string number, DateOnly periodStart, DateOnly periodEnd,
        long subtotalMinor, long taxMinor, string currency, DateOnly dueDate)
    {
        SubscriptionId = subscriptionId;
        Number = number;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        SubtotalMinor = subtotalMinor;
        TaxMinor = taxMinor;
        TotalMinor = subtotalMinor + taxMinor;
        Currency = currency;
        DueDate = dueDate;
        Status = BillingInvoiceStatus.Draft;
    }

    public Guid SubscriptionId { get; private set; }

    /// <summary>Sequential, immutable document number (§10). Unique per tenant.</summary>
    public string Number { get; private set; } = default!;

    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }

    public long SubtotalMinor { get; private set; }

    /// <summary>PPN — zero while the operating entity is not PKP (§10); reserved so it flips on later.</summary>
    public long TaxMinor { get; private set; }

    public long TotalMinor { get; private set; }
    public string Currency { get; private set; } = default!;
    public BillingInvoiceStatus Status { get; private set; }
    public DateOnly DueDate { get; private set; }

    // Gateway linkage + settlement (Slice 3). No card data (§8).
    public string? GatewayInvoiceId { get; private set; }
    public DateTime? PaidAt { get; private set; }

    /// <summary>Reference to the rendered PDF (QuestPDF, Slice 4).</summary>
    public string? PdfRef { get; private set; }

    public static BillingInvoice CreateDraft(Guid subscriptionId, string number, DateOnly periodStart,
        DateOnly periodEnd, long subtotalMinor, long taxMinor, string currency, DateOnly dueDate)
    {
        if (subtotalMinor < 0 || taxMinor < 0)
        {
            throw new ArgumentException("Billing invoice amounts cannot be negative.");
        }

        return new BillingInvoice(subscriptionId, number, periodStart, periodEnd, subtotalMinor, taxMinor,
            currency.Trim().ToUpperInvariant(), dueDate);
    }

    /// <summary>Issues the invoice to the tenant (Draft → Open), optionally linking the gateway invoice.</summary>
    public void Issue(string? gatewayInvoiceId)
    {
        Status = BillingInvoiceStatus.Open;
        GatewayInvoiceId = gatewayInvoiceId;
    }

    /// <summary>Marks the invoice paid (driven by the gateway webhook, §5).</summary>
    public void MarkPaid(DateTime paidAtUtc)
    {
        Status = BillingInvoiceStatus.Paid;
        PaidAt = paidAtUtc;
    }

    public void Void() => Status = BillingInvoiceStatus.Void;
}
