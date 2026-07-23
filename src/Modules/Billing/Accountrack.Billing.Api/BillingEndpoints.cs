using Accountrack.Billing.Application.Features;
using Accountrack.SharedKernel.Results;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Billing.Api;

/// <summary>
/// Billing endpoints (SUBSCRIPTION_BILLING.md §9, ADR-0039). Slice 1 is read-only: the public pricing
/// list and the calling tenant's own subscription. Checkout / plan-change / webhooks land in later slices.
/// </summary>
public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        // Public pricing page — anonymous (§6.2). Exposes only active, public plans from the global catalog.
        app.MapGet("/api/v1/billing/plans", (ISender s, CancellationToken ct) =>
                Send(s.Send(new GetPlansQuery(), ct)))
            .AllowAnonymous().WithTags("Billing").WithName("GetBillingPlans");

        var group = app.MapGroup("/api/v1/billing").WithTags("Billing").RequireAuthorization();

        // The calling tenant's own subscription (tenant resolved by the query filter). Null if never subscribed.
        group.MapGet("/subscription", (ISender s, CancellationToken ct) =>
                Send(s.Send(new GetMySubscriptionQuery(), ct)))
            .RequireAuthorization("Billing.View").WithName("GetMySubscription");

        // What the tenant's plan currently permits, so the SPA can gate UI and show billing banners.
        // The backend stays the hard wall (SECURITY.md §2) — this is a convenience projection.
        group.MapGet("/entitlements", (ISender s, CancellationToken ct) =>
                Send(s.Send(new GetMyEntitlementsQuery(), ct)))
            .RequireAuthorization("Billing.View").WithName("GetMyEntitlements");

        // Start the free trial on a plan (no card, no gateway — §6.2).
        group.MapPost("/subscription/trial", (StartTrialBody b, ISender s, CancellationToken ct) =>
                Created(s.Send(new StartTrialCommand(b.PlanCode), ct), "/api/v1/billing/subscription"))
            .RequireAuthorization("Billing.Manage").WithName("StartBillingTrial");

        return app;
    }

    private static async Task<IResult> Send<T>(Task<Result<T>> task) => (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}

public sealed record StartTrialBody(string PlanCode);
