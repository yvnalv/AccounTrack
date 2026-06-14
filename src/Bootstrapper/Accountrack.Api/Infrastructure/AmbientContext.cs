using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Identity.Infrastructure.Authentication;

namespace Accountrack.Api.Infrastructure;

/// <summary>System UTC clock (CODING_STANDARDS.md §3).</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>Current user resolved from the authenticated principal's JWT claims (SECURITY.md §2).</summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : Guid.Empty;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public bool HasPermission(string permission) =>
        Principal?.HasClaim(AccountrackClaims.Permission, permission) ?? false;
}

/// <summary>
/// Tenant context resolved from JWT claims; the active company comes from the X-Company-Id header
/// and must be among the granted companies (MULTI_TENANCY.md §3). Tenant is never client-supplied.
/// </summary>
public sealed class HttpContextTenantContext : ITenantContext
{
    public const string CompanyHeader = "X-Company-Id";

    private readonly IHttpContextAccessor _accessor;

    public HttpContextTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid TenantId =>
        Guid.TryParse(Principal?.FindFirstValue(AccountrackClaims.TenantId), out var id) ? id : Guid.Empty;

    public IReadOnlyCollection<Guid> GrantedCompanyIds =>
        Principal?.FindAll(AccountrackClaims.Company)
            .Select(c => Guid.TryParse(c.Value, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToArray() ?? Array.Empty<Guid>();

    public Guid CompanyId
    {
        get
        {
            var granted = GrantedCompanyIds;
            var header = _accessor.HttpContext?.Request.Headers[CompanyHeader].ToString();

            if (Guid.TryParse(header, out var requested) && granted.Contains(requested))
            {
                return requested;
            }

            // Fall back to the single granted company when no valid header is supplied.
            return granted.Count == 1 ? granted.First() : Guid.Empty;
        }
    }

    public bool IsSet => IsAuthenticatedTenant();

    private bool IsAuthenticatedTenant() =>
        (Principal?.Identity?.IsAuthenticated ?? false) && TenantId != Guid.Empty;
}
