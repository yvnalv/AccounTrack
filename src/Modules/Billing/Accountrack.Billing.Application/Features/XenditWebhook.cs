using System.Security.Cryptography;
using System.Text;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Integration;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Billing.Application.Features;

/// <summary>
/// Processes a Xendit invoice webhook (SUBSCRIPTION_BILLING.md §5). The <b>webhook is the source of
/// truth for "paid"</b> — never the browser redirect. It is anonymous but authenticated by the
/// per-account <c>x-callback-token</c>, verified here; unverified calls are rejected. Processing is
/// <b>idempotent</b> via <c>platform.InboxState</c> (Xendit retries up to 6× with backoff, so a replay
/// must not double-activate). The handler has no ambient tenant, so it loads the invoice + subscription
/// bypassing the tenant query filter (a reviewed admin path, Rule 33).
/// </summary>
/// <param name="ProvidedToken">The <c>x-callback-token</c> header value from the request.</param>
/// <param name="GatewayInvoiceId">Xendit's invoice id (<c>id</c> in the payload).</param>
/// <param name="Status">Xendit invoice status (e.g. <c>PAID</c>, <c>SETTLED</c>, <c>EXPIRED</c>).</param>
public sealed record ProcessXenditWebhookCommand(string? ProvidedToken, string GatewayInvoiceId, string Status)
    : ICommand;

public sealed class ProcessXenditWebhookHandler : ICommandHandler<ProcessXenditWebhookCommand>
{
    private const string Handler = "billing.xendit.invoice";

    private readonly IBillingInvoiceRepository _invoices;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPlanRepository _plans;
    private readonly IBillingUnitOfWork _uow;
    private readonly IInboxStore _inbox;
    private readonly IXenditWebhookVerifier _verifier;
    private readonly IClock _clock;

    public ProcessXenditWebhookHandler(
        IBillingInvoiceRepository invoices, ISubscriptionRepository subscriptions, IPlanRepository plans,
        IBillingUnitOfWork uow, IInboxStore inbox, IXenditWebhookVerifier verifier, IClock clock)
    {
        _invoices = invoices;
        _subscriptions = subscriptions;
        _plans = plans;
        _uow = uow;
        _inbox = inbox;
        _verifier = verifier;
        _clock = clock;
    }

    public async Task<Result> Handle(ProcessXenditWebhookCommand request, CancellationToken ct)
    {
        // 1. Authenticate the caller by the shared callback token. Reject anything that fails.
        if (!_verifier.IsValid(request.ProvidedToken))
        {
            return BillingErrors.WebhookUnauthorized;
        }

        // 2. Only a paid/settled invoice activates a subscription; other statuses are acknowledged (200)
        //    so the gateway stops retrying, but change nothing.
        if (!IsPaid(request.Status))
        {
            return Result.Success();
        }

        // 3. Idempotency: dedupe on the gateway invoice id (a retry delivers the same id).
        var eventId = DeterministicGuid($"{Handler}:{request.GatewayInvoiceId}");
        if (await _inbox.HasProcessedAsync(Handler, eventId, ct))
        {
            return Result.Success();
        }

        var invoice = await _invoices.GetByGatewayInvoiceIdIgnoringFiltersAsync(request.GatewayInvoiceId, ct);
        if (invoice is null)
        {
            // Unknown invoice — acknowledge so retries stop; nothing to do.
            await _inbox.MarkProcessedAsync(Handler, eventId, ct);
            return Result.Success();
        }

        if (invoice.Status != BillingInvoiceStatus.Paid)
        {
            invoice.MarkPaid(_clock.UtcNow);

            var subscription = await _subscriptions.GetByIdIgnoringFiltersAsync(invoice.SubscriptionId, ct);
            subscription?.Activate(invoice.PeriodStart, invoice.PeriodEnd);

            await _uow.SaveChangesAsync(ct);
        }

        await _inbox.MarkProcessedAsync(Handler, eventId, ct);
        return Result.Success();
    }

    // Xendit reports a completed invoice as PAID and (post-settlement) SETTLED.
    private static bool IsPaid(string status) =>
        status.Equals("PAID", StringComparison.OrdinalIgnoreCase)
        || status.Equals("SETTLED", StringComparison.OrdinalIgnoreCase);

    /// <summary>A stable GUID derived from a string, so the same gateway id always dedupes to one key.</summary>
    private static Guid DeterministicGuid(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}

/// <summary>
/// Verifies the webhook's <c>x-callback-token</c> against the configured account token. Implemented in
/// Infrastructure (where the secret lives); kept as a port so the handler stays testable and free of
/// configuration dependencies.
/// </summary>
public interface IXenditWebhookVerifier
{
    bool IsValid(string? providedToken);
}
