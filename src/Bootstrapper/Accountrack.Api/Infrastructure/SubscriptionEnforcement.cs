using System.Text.Json;
using Accountrack.Billing.Domain;
using Accountrack.Modules.Contracts.Billing;
using Accountrack.Web.Common.Contracts;

namespace Accountrack.Api.Infrastructure;

/// <summary>Options for subscription enforcement (SUBSCRIPTION_BILLING.md §7).</summary>
public sealed class EntitlementOptions
{
    public const string SectionName = "Billing:Entitlements";

    /// <summary>
    /// Master switch. <b>Default off</b> so the guard can be dark-launched: it is deployed and
    /// observable before it ever blocks a customer. Turn on only once plans/checkout are live.
    /// </summary>
    public bool Enforce { get; set; }
}

/// <summary>
/// Blocks business <b>writes</b> when the calling tenant's subscription does not permit them
/// (SUBSCRIPTION_BILLING.md §7): read-only during past-due grace, locked when unpaid/expired.
/// <para>
/// Deliberately narrow, because getting this wrong locks paying customers out of their own data:
/// </para>
/// <list type="bullet">
/// <item>Only mutating verbs are blocked — reads and exports always pass, so a tenant can always
/// retrieve its data (and, at <see cref="TenantAccessLevel.Locked"/>, get it out).</item>
/// <item>Auth, billing and infrastructure routes are always exempt, otherwise a locked-out tenant
/// could never sign in or pay to recover.</item>
/// <item>A tenant with <b>no subscription</b> is unrestricted (grandfathered) — see
/// <c>EntitlementResolver</c>.</item>
/// <item>The whole guard is off unless <see cref="EntitlementOptions.Enforce"/> is set.</item>
/// </list>
/// It complements RBAC rather than replacing it: RBAC decides "may this user", entitlements decide
/// "is this tenant's plan paid and inclusive of this"; both must pass.
/// </summary>
public sealed class SubscriptionEnforcementMiddleware
{
    /// <summary>Route prefixes that must keep working even for a locked tenant.</summary>
    private static readonly string[] ExemptPrefixes =
    {
        "/api/v1/auth",     // sign in / refresh / register — recovery must always be possible
        "/api/v1/billing",  // paying is how a locked tenant recovers
        "/health",
        "/swagger",
    };

    private readonly RequestDelegate _next;
    private readonly EntitlementOptions _options;
    private readonly ILogger<SubscriptionEnforcementMiddleware> _logger;

    public SubscriptionEnforcementMiddleware(
        RequestDelegate next, EntitlementOptions options, ILogger<SubscriptionEnforcementMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantEntitlements entitlements)
    {
        if (!ShouldCheck(context))
        {
            await _next(context);
            return;
        }

        var resolved = await entitlements.GetForCurrentTenantAsync(context.RequestAborted);
        if (resolved.CanWrite)
        {
            await _next(context);
            return;
        }

        _logger.LogInformation(
            "Blocked {Method} {Path}: subscription access level {AccessLevel} (plan {PlanCode}).",
            context.Request.Method, context.Request.Path, resolved.AccessLevel, resolved.PlanCode ?? "-");

        await WriteSubscriptionRequiredAsync(context);
    }

    private bool ShouldCheck(HttpContext context)
    {
        if (!_options.Enforce)
        {
            return false;
        }

        // Anonymous requests have no tenant to evaluate; authentication/authorization handles them.
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (!IsMutating(context.Request.Method))
        {
            return false;
        }

        var path = context.Request.Path;
        foreach (var prefix in ExemptPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsMutating(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));

    private static async Task WriteSubscriptionRequiredAsync(HttpContext context)
    {
        var error = BillingErrors.SubscriptionRequired;
        var body = ApiErrorResponse.From(error.Message, new ApiError(error.Code, null, context.TraceIdentifier));

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}

public static class SubscriptionEnforcementExtensions
{
    public static IServiceCollection AddSubscriptionEnforcement(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(EntitlementOptions.SectionName).Get<EntitlementOptions>()
                      ?? new EntitlementOptions();
        services.AddSingleton(options);
        return services;
    }

    /// <summary>
    /// Must be registered <b>after</b> authentication so the tenant is known, and after authorization
    /// so a request that RBAC would reject anyway never reaches a billing lookup.
    /// </summary>
    public static IApplicationBuilder UseSubscriptionEnforcement(this IApplicationBuilder app) =>
        app.UseMiddleware<SubscriptionEnforcementMiddleware>();
}
