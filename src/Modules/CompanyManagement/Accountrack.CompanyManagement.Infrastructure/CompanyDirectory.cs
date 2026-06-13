using Accountrack.CompanyManagement.Infrastructure.Persistence;
using Accountrack.Modules.Contracts.Company;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.CompanyManagement.Infrastructure;

/// <summary>
/// Implements the public <see cref="ICompanyDirectory"/> contract so other modules can read company
/// configuration (e.g. functional currency) without depending on this module's internals (ADR-0007).
/// </summary>
public sealed class CompanyDirectory : ICompanyDirectory
{
    private readonly CompanyDbContext _db;

    public CompanyDirectory(CompanyDbContext db) => _db = db;

    public async Task<CompanyInfo?> GetAsync(Guid companyId, CancellationToken ct)
    {
        // Tenant-scoped by the global query filter; companyId is the active company.
        var company = await _db.Companies
            .IgnoreQueryFilters()
            .Where(c => c.Id == companyId && !c.IsDeleted)
            .Select(c => new CompanyInfo(c.Id, c.Code, c.FunctionalCurrency, c.FiscalYearStartMonth))
            .FirstOrDefaultAsync(ct);

        return company;
    }
}
