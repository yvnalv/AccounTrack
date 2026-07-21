using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Billing.Application.Abstractions;
using Accountrack.Billing.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Billing.Application.Features;

/// <summary>
/// Starts the calling tenant's free trial on a plan (SUBSCRIPTION_BILLING.md §6.2). No card and no
/// gateway involved — decided 2026-07-11: "no card to start" maximizes conversion for an IDR-first SMB
/// product. The trial grants full access until <c>TrialEndsAt</c>; collecting payment at trial end
/// lands with the Xendit adapter (Slice 3).
/// </summary>
public sealed record StartTrialCommand(string PlanCode) : ICommand<Guid>;

public sealed class StartTrialValidator : AbstractValidator<StartTrialCommand>
{
    public StartTrialValidator() => RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(64);
}

public sealed class StartTrialHandler : ICommandHandler<StartTrialCommand, Guid>
{
    /// <summary>Trial length (SUBSCRIPTION_BILLING.md §13 open item 2 — 14 days assumed).</summary>
    public const int TrialDays = 14;

    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPlanRepository _plans;
    private readonly IBillingUnitOfWork _uow;
    private readonly IClock _clock;

    public StartTrialHandler(
        ISubscriptionRepository subscriptions, IPlanRepository plans, IBillingUnitOfWork uow, IClock clock)
    {
        _subscriptions = subscriptions;
        _plans = plans;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(StartTrialCommand request, CancellationToken ct)
    {
        // One subscription per tenant (enforced by a unique index on TenantId).
        if (await _subscriptions.GetForCurrentTenantAsync(ct) is not null)
        {
            return BillingErrors.AlreadySubscribed;
        }

        var plan = await _plans.GetByCodeAsync(request.PlanCode.Trim().ToUpperInvariant(), ct);
        if (plan is null)
        {
            return BillingErrors.PlanNotFound;
        }

        if (!plan.IsActive)
        {
            return BillingErrors.PlanNotAvailable;
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var subscription = Subscription.StartTrial(
            plan.Id, plan.Interval, PaymentMode.Invoice, today, today.AddDays(TrialDays));

        // TenantId is stamped from the ambient tenant context by the persistence interceptor
        // (MULTI_TENANCY.md) — never set here.
        _subscriptions.Add(subscription);
        await _uow.SaveChangesAsync(ct);

        return subscription.Id;
    }
}
