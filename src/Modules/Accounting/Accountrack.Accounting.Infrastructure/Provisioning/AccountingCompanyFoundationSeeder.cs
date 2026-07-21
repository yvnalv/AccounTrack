using Accountrack.Accounting.Infrastructure.Persistence;
using Accountrack.Accounting.Infrastructure.Seed;
using Accountrack.Modules.Contracts.Company;

namespace Accountrack.Accounting.Infrastructure.Provisioning;

/// <summary>
/// Gives a newly provisioned company its accounting foundation: chart of accounts, fiscal year with
/// open periods, and the default posting rules (BR-CMP-1). Without these, every GL-posting action
/// fails — posting-rule resolution finds nothing and the open-period check rejects the journal.
/// Runs first (<see cref="Order"/> 100) because other modules' defaults reference its rule keys.
/// </summary>
public sealed class AccountingCompanyFoundationSeeder : ICompanyFoundationSeeder
{
    private readonly AccountingDbContext _db;

    public AccountingCompanyFoundationSeeder(AccountingDbContext db) => _db = db;

    public int Order => 100;

    public Task SeedAsync(CompanyFoundation company, CancellationToken ct) =>
        AccountingDataSeeder.SeedForCompanyAsync(
            _db, company.TenantId, company.CompanyId, company.Year, company.FiscalYearStartMonth, ct);
}
