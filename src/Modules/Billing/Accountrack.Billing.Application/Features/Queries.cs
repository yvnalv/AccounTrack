using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Application.Contracts;
using Accountrack.Billing.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Billing.Application.Features;

/// <summary>Public pricing page: the active, public plans (SUBSCRIPTION_BILLING.md §9).</summary>
public sealed record GetPlansQuery : IQuery<IReadOnlyList<PlanDto>>;

public sealed class GetPlansHandler : IQueryHandler<GetPlansQuery, IReadOnlyList<PlanDto>>
{
    private readonly IPlanRepository _plans;
    public GetPlansHandler(IPlanRepository plans) => _plans = plans;

    public async Task<Result<IReadOnlyList<PlanDto>>> Handle(GetPlansQuery request, CancellationToken ct)
    {
        var plans = await _plans.ListPublicAsync(ct);
        return Result.Success<IReadOnlyList<PlanDto>>(plans.Select(ToDto).ToList());
    }

    internal static PlanDto ToDto(Plan p) => new(
        p.Id, p.Code, p.Name, p.Interval, p.BasePriceMinor, p.IncludedSeats, p.PerSeatPriceMinor,
        p.MaxCompanies, p.Currency, p.FeaturesJson, p.IsActive, p.IsPublic);
}

/// <summary>The calling tenant's subscription (tenant resolved by the query filter), or null.</summary>
public sealed record GetMySubscriptionQuery : IQuery<SubscriptionDto?>;

public sealed class GetMySubscriptionHandler : IQueryHandler<GetMySubscriptionQuery, SubscriptionDto?>
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPlanRepository _plans;

    public GetMySubscriptionHandler(ISubscriptionRepository subscriptions, IPlanRepository plans)
    {
        _subscriptions = subscriptions;
        _plans = plans;
    }

    public async Task<Result<SubscriptionDto?>> Handle(GetMySubscriptionQuery request, CancellationToken ct)
    {
        var sub = await _subscriptions.GetForCurrentTenantAsync(ct);
        if (sub is null)
        {
            return Result.Success<SubscriptionDto?>(null);
        }

        var plan = await _plans.GetByIdAsync(sub.PlanId, ct);
        return Result.Success<SubscriptionDto?>(new SubscriptionDto(
            sub.Id, sub.PlanId, plan?.Code ?? string.Empty, plan?.Name ?? string.Empty, sub.Status,
            sub.Interval, sub.ExtraSeats, sub.PaymentMode, sub.TrialEndsAt, sub.CurrentPeriodStart,
            sub.CurrentPeriodEnd, sub.CancelAtPeriodEnd));
    }
}
