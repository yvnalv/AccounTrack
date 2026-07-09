using System.Text.Json;
using System.Threading.RateLimiting;
using Accountrack.Web.Common.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Accountrack.Api.Infrastructure;

/// <summary>
/// Brute-force / credential-stuffing protection for the anonymous authentication endpoints
/// (SECURITY.md §5). Applies a per-client fixed window to <c>/auth/login</c>, <c>/auth/register</c>
/// and <c>/auth/refresh</c>; requests over the limit get <c>429</c> in the standard failure envelope
/// with a <c>Retry-After</c> hint. Limits are configurable under <c>RateLimiting:Auth</c>.
/// </summary>
public static class AuthRateLimiting
{
    /// <summary>Named policy — must match <see cref="Accountrack.Identity.Api.IdentityEndpoints.RateLimitPolicy"/>.</summary>
    public const string PolicyName = "auth";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        // Defaults tolerate an office behind a shared NAT (per-IP) while still shutting down the
        // thousands-per-minute rate a brute-force needs. Tighten via appsettings if required.
        var permitLimit = config.GetValue("RateLimiting:Auth:PermitLimit", 20);
        var windowSeconds = config.GetValue("RateLimiting:Auth:WindowSeconds", 60);

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ClientKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                    }));

            options.OnRejected = async (context, ct) =>
            {
                var response = context.HttpContext.Response;
                if (response.HasStarted)
                {
                    return;
                }

                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                response.ContentType = "application/json";
                var body = ApiErrorResponse.From(
                    "Too many attempts. Please wait a moment and try again.",
                    new ApiError("RATE_LIMITED", null, context.HttpContext.TraceIdentifier));
                await response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions), ct);
            };
        });

        return services;
    }

    // Best-effort client identity behind the reverse proxy: the leftmost X-Forwarded-For hop, then
    // X-Real-IP, then the socket address. NOTE: X-Forwarded-For is client-supplied and therefore
    // spoofable; fully robust per-IP limiting additionally requires the trusted edge proxy to
    // overwrite (not append) the client-facing hop. (SECURITY.md §5.)
    private static string ClientKey(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp.Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
