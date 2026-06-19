using Accountrack.Application.Abstractions.Context;

namespace Accountrack.IntegrationTests.Infrastructure;

/// <summary>
/// Mutable ambient tenant context for tests. Mirrors the production contract: a query/save sees only
/// the tenant + company set here. <see cref="IsSet"/> follows whether a tenant has been established.
/// </summary>
public sealed class FakeTenantContext : ITenantContext
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public IReadOnlyCollection<Guid> GrantedCompanyIds { get; set; } = Array.Empty<Guid>();
    public bool IsSet => TenantId != Guid.Empty;

    public static FakeTenantContext For(Guid tenantId, Guid companyId) =>
        new() { TenantId = tenantId, CompanyId = companyId, GrantedCompanyIds = new[] { companyId } };

    /// <summary>An unestablished context — as for a background job with no authenticated principal.</summary>
    public static FakeTenantContext None() => new();
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public bool IsAuthenticated => UserId != Guid.Empty;
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    public bool HasPermission(string permission) => true;
}

public sealed class FixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
