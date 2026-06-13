namespace Accountrack.Application.Abstractions.Context;

/// <summary>
/// Ambient tenant context for the current operation, resolved from the authenticated principal
/// (docs/MULTI_TENANCY.md §3). Implemented in Infrastructure; never populated from client input.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }

    /// <summary>The active company for this operation.</summary>
    Guid CompanyId { get; }

    /// <summary>The companies the current user is granted access to within the tenant.</summary>
    IReadOnlyCollection<Guid> GrantedCompanyIds { get; }

    bool IsSet { get; }
}

/// <summary>The authenticated user for the current operation.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }

    bool IsAuthenticated { get; }

    bool HasPermission(string permission);
}

/// <summary>
/// Abstraction over the system clock (UTC). Domain/application code must use this instead of
/// <c>DateTime.Now</c> so behavior is deterministic and testable (CODING_STANDARDS.md §3).
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
