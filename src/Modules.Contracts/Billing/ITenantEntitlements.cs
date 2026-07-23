namespace Accountrack.Modules.Contracts.Billing;

/// <summary>
/// What a tenant's subscription currently permits (SUBSCRIPTION_BILLING.md §7). This is
/// <b>orthogonal to RBAC</b>: RBAC answers "may this user do X", entitlements answer "does this
/// tenant's plan include X, and is it paid". Both must pass.
/// </summary>
public enum TenantAccessLevel
{
    /// <summary>Trialing or paid — normal operation.</summary>
    Full = 0,

    /// <summary>
    /// Past-due grace (decided 2026-07-11): the tenant may read and export its data, but business
    /// writes are blocked so there is real pressure to pay without risking data loss.
    /// </summary>
    ReadOnly = 1,

    /// <summary>Unpaid or expired — business writes blocked; only auth, billing and reads remain.</summary>
    Locked = 2,
}

/// <summary>
/// The resolved entitlements for the calling tenant. Derived from its subscription + plan; exposed to
/// the SPA so it can hide gated UI, but the backend remains the hard wall (mirrors RBAC, SECURITY.md §2).
/// </summary>
/// <param name="HasSubscription">
/// False when the tenant has never subscribed. Such tenants are <b>grandfathered</b> (treated as
/// <see cref="TenantAccessLevel.Full"/>) so enabling billing never locks out existing customers.
/// </param>
/// <param name="MaxUsers">Included + purchased seats; null means unlimited.</param>
/// <param name="MaxCompanies">Companies the plan allows; null means unlimited.</param>
public sealed record TenantEntitlements(
    bool HasSubscription,
    TenantAccessLevel AccessLevel,
    string? PlanCode,
    string? PlanName,
    string? Status,
    DateOnly? TrialEndsAt,
    DateOnly? CurrentPeriodEnd,
    int? MaxUsers,
    int? MaxCompanies,
    IReadOnlyDictionary<string, bool> Features)
{
    /// <summary>Business writes are only allowed at <see cref="TenantAccessLevel.Full"/>.</summary>
    public bool CanWrite => AccessLevel == TenantAccessLevel.Full;

    /// <summary>Whether the plan includes a named feature flag (unknown flags are treated as off).</summary>
    public bool HasFeature(string key) => Features.TryGetValue(key, out var on) && on;
}

/// <summary>
/// Resolves the calling tenant's entitlements. Implemented by the Billing module; consumed by the
/// host's enforcement middleware and by modules that must respect plan limits (ADR-0007, Rule 27).
/// </summary>
public interface ITenantEntitlements
{
    Task<TenantEntitlements> GetForCurrentTenantAsync(CancellationToken ct);
}
