using System.Text.Json;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Domain;
using Accountrack.Modules.Contracts.Billing;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Billing.Application.Features;

/// <summary>
/// Resolves the calling tenant's entitlements from its subscription + plan (SUBSCRIPTION_BILLING.md §7).
/// <para>
/// <b>Grandfathering:</b> a tenant with no subscription row resolves to
/// <see cref="TenantAccessLevel.Full"/> with no limits. Every tenant that existed before billing was
/// introduced is in exactly that state, so enforcement can be switched on without locking anyone out;
/// they become restricted only once a subscription exists and lapses.
/// </para>
/// </summary>
public sealed class EntitlementResolver : ITenantEntitlements
{
    private static readonly IReadOnlyDictionary<string, bool> NoFeatures =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPlanRepository _plans;

    public EntitlementResolver(ISubscriptionRepository subscriptions, IPlanRepository plans)
    {
        _subscriptions = subscriptions;
        _plans = plans;
    }

    /// <summary>Entitlements for a tenant that has never subscribed — unrestricted (see class remarks).</summary>
    public static TenantEntitlements Unsubscribed { get; } = new(
        HasSubscription: false, TenantAccessLevel.Full, PlanCode: null, PlanName: null, Status: null,
        TrialEndsAt: null, CurrentPeriodEnd: null, MaxUsers: null, MaxCompanies: null, NoFeatures);

    public async Task<TenantEntitlements> GetForCurrentTenantAsync(CancellationToken ct)
    {
        var subscription = await _subscriptions.GetForCurrentTenantAsync(ct);
        if (subscription is null)
        {
            return Unsubscribed;
        }

        var plan = await _plans.GetByIdAsync(subscription.PlanId, ct);
        return Build(subscription, plan);
    }

    /// <summary>Pure projection of a subscription (+ its plan) onto entitlements. Unit-testable.</summary>
    public static TenantEntitlements Build(Subscription subscription, Plan? plan) => new(
        HasSubscription: true,
        AccessLevelFor(subscription.Status),
        plan?.Code,
        plan?.Name,
        subscription.Status.ToString(),
        subscription.TrialEndsAt,
        subscription.CurrentPeriodEnd,
        MaxUsers: plan is null ? null : plan.IncludedSeats + subscription.ExtraSeats,
        MaxCompanies: plan?.MaxCompanies,
        Features: ParseFeatures(plan?.FeaturesJson));

    /// <summary>
    /// Status → access level (SUBSCRIPTION_BILLING.md §6.1, decided 2026-07-11). A cancelled
    /// subscription keeps full access until the period actually ends, at which point the lifecycle
    /// moves it to <see cref="SubscriptionStatus.Expired"/>.
    /// </summary>
    public static TenantAccessLevel AccessLevelFor(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.Trialing => TenantAccessLevel.Full,
        SubscriptionStatus.Active => TenantAccessLevel.Full,
        SubscriptionStatus.Canceled => TenantAccessLevel.Full,
        SubscriptionStatus.PastDue => TenantAccessLevel.ReadOnly,
        SubscriptionStatus.Unpaid => TenantAccessLevel.Locked,
        SubscriptionStatus.Expired => TenantAccessLevel.Locked,
        _ => TenantAccessLevel.Locked,
    };

    /// <summary>Plan feature flags are stored as a flat JSON object; malformed JSON degrades to none.</summary>
    private static IReadOnlyDictionary<string, bool> ParseFeatures(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return NoFeatures;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
            return parsed is null
                ? NoFeatures
                : new Dictionary<string, bool>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return NoFeatures;
        }
    }
}

/// <summary>The calling tenant's entitlements, for the SPA to hide gated UI and show billing banners.</summary>
public sealed record GetMyEntitlementsQuery : IQuery<TenantEntitlements>;

public sealed class GetMyEntitlementsHandler : IQueryHandler<GetMyEntitlementsQuery, TenantEntitlements>
{
    private readonly ITenantEntitlements _entitlements;
    public GetMyEntitlementsHandler(ITenantEntitlements entitlements) => _entitlements = entitlements;

    public async Task<Result<TenantEntitlements>> Handle(GetMyEntitlementsQuery request, CancellationToken ct) =>
        Result.Success(await _entitlements.GetForCurrentTenantAsync(ct));
}
