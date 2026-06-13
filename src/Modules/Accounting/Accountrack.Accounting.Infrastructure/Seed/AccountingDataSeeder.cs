using Accountrack.Accounting.Domain;
using Accountrack.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Accounting.Infrastructure.Seed;

/// <summary>
/// Seeds a default Indonesian-SMB chart of accounts and the current fiscal year for the dev
/// company, so manual journals can be posted out of the box. Account codes match the defaults in
/// docs/POSTING_RULES.md §2. Ids match the Company/Identity dev seed.
/// </summary>
public static class AccountingDataSeeder
{
    private static readonly Guid DevTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DevCompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task SeedAsync(AccountingDbContext db, int currentYear, CancellationToken ct = default)
    {
        await SeedChartAsync(db, ct);
        await SeedFiscalYearAsync(db, currentYear, ct);
    }

    private static async Task SeedChartAsync(AccountingDbContext db, CancellationToken ct)
    {
        var hasAccounts = await db.Accounts.IgnoreQueryFilters()
            .AnyAsync(a => a.CompanyId == DevCompanyId && !a.IsDeleted, ct);
        if (hasAccounts)
        {
            return;
        }

        (string Code, string Name, AccountType Type, bool Control, ControlType Ct)[] chart =
        {
            ("1000", "Cash", AccountType.Asset, false, ControlType.None),
            ("1010", "Bank", AccountType.Asset, false, ControlType.None),
            ("1100", "Accounts Receivable", AccountType.Asset, true, ControlType.AccountsReceivable),
            ("1200", "Inventory", AccountType.Asset, true, ControlType.Inventory),
            ("1300", "VAT Input (PPN Masukan)", AccountType.Asset, false, ControlType.None),
            ("1400", "Supplier Advances", AccountType.Asset, false, ControlType.None),
            ("2100", "Accounts Payable", AccountType.Liability, true, ControlType.AccountsPayable),
            ("2150", "Goods Received / Invoice Received", AccountType.Liability, false, ControlType.None),
            ("2300", "VAT Output (PPN Keluaran)", AccountType.Liability, false, ControlType.None),
            ("2400", "Customer Advances", AccountType.Liability, false, ControlType.None),
            ("3900", "Retained Earnings", AccountType.Equity, false, ControlType.None),
            ("4000", "Sales Revenue", AccountType.Revenue, false, ControlType.None),
            ("5000", "Cost of Goods Sold", AccountType.Expense, false, ControlType.None),
            ("5100", "Inventory Variance", AccountType.Expense, false, ControlType.None),
            ("7900", "Rounding Difference", AccountType.Expense, false, ControlType.None),
        };

        foreach (var a in chart)
        {
            var account = Account.Create(a.Code, a.Name, a.Type, isControlAccount: a.Control, controlType: a.Ct, isSystem: true);
            account.TenantId = DevTenantId;
            account.CompanyId = DevCompanyId;
            db.Accounts.Add(account);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedFiscalYearAsync(AccountingDbContext db, int year, CancellationToken ct)
    {
        var hasYear = await db.FiscalYears.IgnoreQueryFilters()
            .AnyAsync(fy => fy.CompanyId == DevCompanyId && fy.Year == year && !fy.IsDeleted, ct);
        if (hasYear)
        {
            return;
        }

        var fiscalYear = FiscalYear.Create(year, startMonth: 1);
        fiscalYear.TenantId = DevTenantId;
        fiscalYear.CompanyId = DevCompanyId;
        foreach (var period in fiscalYear.Periods)
        {
            period.TenantId = DevTenantId;
            period.CompanyId = DevCompanyId;
        }

        db.FiscalYears.Add(fiscalYear);
        await db.SaveChangesAsync(ct);
    }
}
