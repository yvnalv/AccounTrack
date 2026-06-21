using Accountrack.CompanyManagement.Domain;
using Accountrack.CompanyManagement.Infrastructure.Persistence;
using Accountrack.Modules.Contracts.Company;

namespace Accountrack.CompanyManagement.Infrastructure;

/// <summary>
/// Implements <see cref="ICompanyProvisioning"/> so the Identity registration flow can create a new
/// tenant + first company without depending on this module's internals (ADR-0007). The tenant is the
/// tenancy root (no tenant filter); the company is created under it.
/// </summary>
public sealed class CompanyProvisioning : ICompanyProvisioning
{
    private readonly CompanyDbContext _db;

    public CompanyProvisioning(CompanyDbContext db) => _db = db;

    public async Task<Guid> ProvisionTenantAsync(
        Guid tenantId, string organizationName, string companyCode, string companyName,
        string functionalCurrency, int fiscalYearStartMonth, string timeZone, CancellationToken ct)
    {
        _db.Tenants.Add(Tenant.CreateWithId(tenantId, organizationName));

        var company = Company.Create(
            tenantId, companyCode, companyName, functionalCurrency, fiscalYearStartMonth, timeZone,
            isVatRegistered: false);
        _db.Companies.Add(company);

        await _db.SaveChangesAsync(ct);
        return company.Id;
    }
}
