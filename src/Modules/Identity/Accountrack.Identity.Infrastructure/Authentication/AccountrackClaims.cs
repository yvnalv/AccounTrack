namespace Accountrack.Identity.Infrastructure.Authentication;

/// <summary>
/// Custom JWT claim types used across Accountrack. The host's tenant/current-user adapters read
/// the same constants (MULTI_TENANCY.md §8, SECURITY.md §2).
/// </summary>
public static class AccountrackClaims
{
    public const string TenantId = "tenant_id";

    /// <summary>One claim per granted company id.</summary>
    public const string Company = "company";

    /// <summary>One claim per permission code.</summary>
    public const string Permission = "perm";
}
