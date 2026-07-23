using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Application.Contracts;
using Accountrack.Billing.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Billing.Application.Features;

/// <summary>
/// Starts checkout for the calling tenant's subscription (SUBSCRIPTION_BILLING.md §4/§6.2): bills the
/// subscription's current plan for the next period by creating a <see cref="BillingInvoice"/> and a
/// hosted gateway invoice, and returns the pay URL. The subscription activates only when the
/// <b>webhook</b> confirms payment (the source of truth, §5) — never here, never on the browser redirect.
/// Requires an existing subscription (start a trial first). An optional <paramref name="PlanCode"/>
/// switches the subscription to a different plan (upgrade/downgrade) before billing it.
/// </summary>
public sealed record SubscribeCommand(string? PlanCode = null) : ICommand<CheckoutDto>;

public sealed class SubscribeHandler : ICommandHandler<SubscribeCommand, CheckoutDto>
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPlanRepository _plans;
    private readonly IBillingInvoiceRepository _invoices;
    private readonly IPaymentGateway _gateway;
    private readonly IBillingUnitOfWork _uow;
    private readonly IClock _clock;

    public SubscribeHandler(
        ISubscriptionRepository subscriptions, IPlanRepository plans, IBillingInvoiceRepository invoices,
        IPaymentGateway gateway, IBillingUnitOfWork uow, IClock clock)
    {
        _subscriptions = subscriptions;
        _plans = plans;
        _invoices = invoices;
        _gateway = gateway;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<CheckoutDto>> Handle(SubscribeCommand request, CancellationToken ct)
    {
        var subscription = await _subscriptions.GetForCurrentTenantAsync(ct);
        if (subscription is null)
        {
            return BillingErrors.NoSubscription;
        }

        Plan? plan;
        if (!string.IsNullOrWhiteSpace(request.PlanCode))
        {
            // Switching to a chosen plan (upgrade/downgrade) before paying.
            plan = await _plans.GetByCodeAsync(request.PlanCode.Trim().ToUpperInvariant(), ct);
            if (plan is null)
            {
                return BillingErrors.PlanNotFound;
            }

            if (!plan.IsActive)
            {
                return BillingErrors.PlanNotAvailable;
            }

            if (plan.Id != subscription.PlanId)
            {
                subscription.ChangePlan(plan.Id, plan.Interval);
            }
        }
        else
        {
            plan = await _plans.GetByIdAsync(subscription.PlanId, ct);
            if (plan is null)
            {
                return BillingErrors.PlanNotFound;
            }
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var periodEnd = plan.Interval == BillingInterval.Annual ? today.AddYears(1) : today.AddMonths(1);
        var amountMinor = plan.BasePriceMinor + subscription.ExtraSeats * plan.PerSeatPriceMinor;

        // Per-tenant document number. A count-based sequence is fine at MVP volumes; the unique index on
        // (TenantId, Number) is the backstop. (A dedicated sequence can replace it later.)
        var seq = await _invoices.CountForCurrentTenantAsync(ct) + 1;
        var number = $"SUB-{today:yyyyMM}-{seq:D4}";

        var invoice = BillingInvoice.CreateDraft(
            subscription.Id, number, today, periodEnd, subtotalMinor: amountMinor, taxMinor: 0,
            plan.Currency, dueDate: today.AddDays(7));
        _invoices.Add(invoice);
        // Persist first so the invoice id is a stable external reference for the gateway + webhook.
        await _uow.SaveChangesAsync(ct);

        var created = await _gateway.CreateInvoiceAsync(
            new CreateGatewayInvoiceRequest(
                ExternalId: invoice.Id.ToString(),
                AmountMinor: amountMinor,
                Currency: plan.Currency,
                Description: $"Accountrack — {plan.Name} ({today:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd})",
                PayerEmail: null,
                SuccessRedirectUrl: null,   // the adapter applies configured defaults
                FailureRedirectUrl: null),
            ct);

        if (created.IsFailure)
        {
            return created.Error;
        }

        invoice.Issue(created.Value.Id);
        await _uow.SaveChangesAsync(ct);

        return new CheckoutDto(invoice.Id, created.Value.InvoiceUrl, amountMinor, plan.Currency);
    }
}
