using Accountrack.Application.Abstractions.Context;

namespace Accountrack.Api.Infrastructure;

/// <summary>System UTC clock (CODING_STANDARDS.md §3).</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// Placeholder current-user adapter. Replaced by a JWT/HttpContext-backed implementation when
/// the Identity module lands (SECURITY.md). For now reports an unauthenticated principal.
/// </summary>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public Guid UserId => Guid.Empty;

    public bool IsAuthenticated => false;

    public bool HasPermission(string permission) => false;
}

/// <summary>
/// Placeholder tenant context. Replaced by an HttpContext/JWT-backed implementation
/// (docs/MULTI_TENANCY.md §3) once Identity + Company modules exist. Reports an unset context.
/// </summary>
public sealed class UnsetTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;

    public Guid CompanyId => Guid.Empty;

    public IReadOnlyCollection<Guid> GrantedCompanyIds => Array.Empty<Guid>();

    public bool IsSet => false;
}
