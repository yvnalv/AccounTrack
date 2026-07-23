namespace Accountrack.Billing.Infrastructure.Payments;

/// <summary>
/// Xendit configuration (SUBSCRIPTION_BILLING.md §3.4). Secrets come from configuration/environment, never
/// source (CLAUDE.md Non-Negotiables). In dev/sandbox the secret key is prefixed <c>xnd_development_</c>.
/// </summary>
public sealed class XenditOptions
{
    public const string SectionName = "Billing:Xendit";

    /// <summary>Secret API key (Basic-auth username). <c>xnd_development_…</c> in test mode.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Per-account webhook verification token (the <c>x-callback-token</c> header value).</summary>
    public string CallbackToken { get; set; } = string.Empty;

    /// <summary>Xendit API base URL. Same host for test and live — the key decides the mode.</summary>
    public string BaseUrl { get; set; } = "https://api.xendit.co";

    /// <summary>Where Xendit redirects the browser after a successful / failed payment (optional).</summary>
    public string? SuccessRedirectUrl { get; set; }
    public string? FailureRedirectUrl { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}
