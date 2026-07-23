using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Application.Features;
using Accountrack.Billing.Domain;
using Accountrack.SharedKernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Accountrack.Billing.Infrastructure.Payments;

/// <summary>
/// Xendit adapter for <see cref="IPaymentGateway"/> — creates a hosted invoice via the Invoices API
/// (SUBSCRIPTION_BILLING.md §3/§3.4, ADR-0039). Authenticates with HTTP Basic using the secret key as the
/// username and a blank password. IDR amounts are whole rupiah (the currency's minor unit).
/// </summary>
public sealed class XenditGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly XenditOptions _options;
    private readonly ILogger<XenditGateway> _logger;

    public XenditGateway(HttpClient http, IOptions<XenditOptions> options, ILogger<XenditGateway> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<GatewayInvoice>> CreateInvoiceAsync(
        CreateGatewayInvoiceRequest request, CancellationToken ct)
    {
        if (!_options.IsConfigured)
        {
            return BillingErrors.GatewayError("Xendit is not configured (missing Billing:Xendit:SecretKey).");
        }

        var body = new XenditCreateInvoice(
            ExternalId: request.ExternalId,
            Amount: request.AmountMinor,
            Currency: request.Currency,
            Description: request.Description,
            PayerEmail: request.PayerEmail,
            SuccessRedirectUrl: request.SuccessRedirectUrl ?? _options.SuccessRedirectUrl,
            FailureRedirectUrl: request.FailureRedirectUrl ?? _options.FailureRedirectUrl);

        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, "/v2/invoices")
            {
                Content = JsonContent.Create(body),
            };
            // Basic auth: secret key as username, blank password.
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.SecretKey}:"));
            msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            using var response = await _http.SendAsync(msg, ct);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Xendit invoice creation failed ({Status}): {Detail}",
                    (int)response.StatusCode, detail);
                return BillingErrors.GatewayError($"HTTP {(int)response.StatusCode}");
            }

            var created = await response.Content.ReadFromJsonAsync<XenditInvoiceResponse>(ct);
            if (created is null || string.IsNullOrEmpty(created.Id) || string.IsNullOrEmpty(created.InvoiceUrl))
            {
                return BillingErrors.GatewayError("Malformed response from Xendit.");
            }

            return new GatewayInvoice(created.Id, created.InvoiceUrl, created.Status ?? "PENDING");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Xendit invoice creation errored.");
            return BillingErrors.GatewayError(ex.Message);
        }
    }

    // --- Xendit Invoices API DTOs (snake_case) ---

    private sealed record XenditCreateInvoice(
        [property: JsonPropertyName("external_id")] string ExternalId,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("payer_email")] string? PayerEmail,
        [property: JsonPropertyName("success_redirect_url")] string? SuccessRedirectUrl,
        [property: JsonPropertyName("failure_redirect_url")] string? FailureRedirectUrl);

    private sealed record XenditInvoiceResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("invoice_url")] string InvoiceUrl,
        [property: JsonPropertyName("status")] string? Status);
}

/// <summary>
/// Constant-time comparison of the webhook's <c>x-callback-token</c> against the configured token
/// (SUBSCRIPTION_BILLING.md §8). Rejects when unconfigured or mismatched.
/// </summary>
public sealed class XenditWebhookVerifier : IXenditWebhookVerifier
{
    private readonly XenditOptions _options;
    public XenditWebhookVerifier(IOptions<XenditOptions> options) => _options = options.Value;

    public bool IsValid(string? providedToken)
    {
        if (string.IsNullOrEmpty(providedToken) || string.IsNullOrEmpty(_options.CallbackToken))
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(providedToken);
        var b = Encoding.UTF8.GetBytes(_options.CallbackToken);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
    }
}
