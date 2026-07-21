using Accountrack.Expenses.Infrastructure.Persistence;
using Accountrack.Expenses.Infrastructure.Seed;
using Accountrack.Modules.Contracts.Company;

namespace Accountrack.Expenses.Infrastructure.Provisioning;

/// <summary>
/// Gives a newly provisioned company the default expense categories (BR-CMP-1). Each category's
/// posting-rule key matches a rule seeded by the Accounting foundation seeder, so expense vouchers
/// resolve to their GL account out of the box — hence this runs after Accounting.
/// </summary>
public sealed class ExpensesCompanyFoundationSeeder : ICompanyFoundationSeeder
{
    private readonly ExpensesDbContext _db;

    public ExpensesCompanyFoundationSeeder(ExpensesDbContext db) => _db = db;

    public int Order => 300;

    public Task SeedAsync(CompanyFoundation company, CancellationToken ct) =>
        ExpensesSeeder.SeedForCompanyAsync(_db, company.TenantId, company.CompanyId, ct);
}
