using System.Text.Json.Serialization;
using Accountrack.Billing.Application.Features;
using Accountrack.SharedKernel.Results;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Billing.Api;

/// <summary>
/// Billing endpoints (SUBSCRIPTION_BILLING.md §9, ADR-0039): the public pricing list, the calling
/// tenant's subscription/entitlements, the free trial, hosted-checkout, and the Xendit payment webhook.
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

        // Start checkout: bill the subscription's plan for the next period, return the hosted pay URL.
        group.MapPost("/subscription/checkout", (ISender s, CancellationToken ct) =>
                Send(s.Send(new SubscribeCommand(), ct)))
            .RequireAuthorization("Billing.Manage").WithName("StartBillingCheckout");

        // Xendit payment webhook — the source of truth for "paid" (§5). Anonymous but authenticated by the
        // x-callback-token header (verified in the handler); processed idempotently. Must live outside the
        // authenticated group and stay exempt from the subscription-enforcement middleware.
        app.MapPost("/api/v1/billing/webhooks/xendit", async (
                XenditWebhookPayload payload, HttpRequest req, ISender s, CancellationToken ct) =>
            {
                var token = req.Headers["x-callback-token"].ToString();
                var result = await s.Send(new ProcessXenditWebhookCommand(token, payload.Id ?? "", payload.Status ?? ""), ct);
                return result.ToHttpResult();
            })
            .AllowAnonymous().WithTags("Billing").WithName("XenditWebhook");

        return app;
    }

    private static async Task<IResult> Send<T>(Task<Result<T>> task) => (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}

public sealed record StartTrialBody(string PlanCode);

/// <summary>The subset of Xendit's invoice-webhook body we act on (SUBSCRIPTION_BILLING.md §5).</summary>
public sealed record XenditWebhookPayload(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("external_id")] string? ExternalId);
