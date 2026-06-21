namespace Accountrack.Modules.Contracts.Company;

/// <summary>
/// Public contract for provisioning a brand-new tenant + its first company during public
/// organization sign-up (ADR-0007). Implemented by Company Management; the caller (Identity's
/// registration use case) then seeds roles and the administrator user for the returned tenant.
/// </summary>
public interface ICompanyProvisioning
{
    /// <summary>
    /// Creates the tenant and its first company, persisting them, and returns the new company id.
    /// </summary>
    Task<Guid> ProvisionTenantAsync(
        Guid tenantId, string organizationName, string companyCode, string companyName,
        string functionalCurrency, int fiscalYearStartMonth, string timeZone, CancellationToken ct);
}
