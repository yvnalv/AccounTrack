using Accountrack.SharedKernel.Results;

namespace Accountrack.Billing.Application.Abstractions;

/// <summary>
/// Provider-abstraction port for the payment gateway (SUBSCRIPTION_BILLING.md §5, ADR-0039). Billing
/// depends only on this; the Xendit adapter lives in Infrastructure, so switching or running a second
/// provider later is mechanical. Slice 3 needs only hosted-invoice creation; tokenized auto-charge is a
/// later phase.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Creates a hosted invoice (a pay page) and returns its id + pay URL.</summary>
    Task<Result<GatewayInvoice>> CreateInvoiceAsync(CreateGatewayInvoiceRequest request, CancellationToken ct);
}

/// <summary>Request to create a hosted invoice. Amount is in the currency's minor units (IDR = rupiah).</summary>
public sealed record CreateGatewayInvoiceRequest(
    string ExternalId,
    long AmountMinor,
    string Currency,
    string Description,
    string? PayerEmail,
    string? SuccessRedirectUrl,
    string? FailureRedirectUrl);

/// <summary>The gateway's hosted invoice: its id, the URL the customer pays at, and its status.</summary>
public sealed record GatewayInvoice(string Id, string InvoiceUrl, string Status);
