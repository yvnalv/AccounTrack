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
        await SeedPostingRulesAsync(db, ct);
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
            ("6000", "Electricity & Utilities", AccountType.Expense, false, ControlType.None),
            ("6100", "Transportation", AccountType.Expense, false, ControlType.None),
            ("6200", "Rent", AccountType.Expense, false, ControlType.None),
            ("6300", "Office Supplies", AccountType.Expense, false, ControlType.None),
            ("6400", "Salaries & Wages", AccountType.Expense, false, ControlType.None),
            ("6900", "Other Operating Expense", AccountType.Expense, false, ControlType.None),
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

    /// <summary>
    /// Seeds the company-wide default posting rules (docs/POSTING_RULES.md §2): each required
    /// <see cref="PostingRuleKeys"/> maps to its default seeded account. CashBank is intentionally
    /// not seeded as a single default — it is resolved per chosen bank/cash account via a selector.
    /// </summary>
    private static async Task SeedPostingRulesAsync(AccountingDbContext db, CancellationToken ct)
    {
        var hasRules = await db.PostingRules.IgnoreQueryFilters()
            .AnyAsync(r => r.CompanyId == DevCompanyId && !r.IsDeleted, ct);
        if (hasRules)
        {
            return;
        }

        (string Key, string AccountCode)[] defaults =
        {
            (PostingRuleKeys.ArControl, "1100"),
            (PostingRuleKeys.ApControl, "2100"),
            (PostingRuleKeys.Inventory, "1200"),
            (PostingRuleKeys.GrIrClearing, "2150"),
            (PostingRuleKeys.Revenue, "4000"),
            (PostingRuleKeys.Cogs, "5000"),
            (PostingRuleKeys.VatOutput, "2300"),
            (PostingRuleKeys.VatInput, "1300"),
            (PostingRuleKeys.InventoryVariance, "5100"),
            (PostingRuleKeys.Rounding, "7900"),
            (PostingRuleKeys.CustomerAdvance, "2400"),
            (PostingRuleKeys.SupplierAdvance, "1400"),
            (PostingRuleKeys.RetainedEarnings, "3900"),
            // Expense categories (ADR-0030). Keys match Expenses module's seeded categories.
            ("EXPENSE.ELECTRICITY", "6000"),
            ("EXPENSE.TRANSPORT", "6100"),
            ("EXPENSE.RENT", "6200"),
            ("EXPENSE.SUPPLIES", "6300"),
            ("EXPENSE.SALARIES", "6400"),
            ("EXPENSE.OTHER", "6900"),
        };

        var accountsByCode = await db.Accounts.IgnoreQueryFilters()
            .Where(a => a.CompanyId == DevCompanyId && !a.IsDeleted)
            .ToDictionaryAsync(a => a.Code, a => a.Id, ct);

        foreach (var (key, code) in defaults)
        {
            if (!accountsByCode.TryGetValue(code, out var accountId))
            {
                continue;
            }

            var rule = PostingRule.CreateDefault(key, accountId);
            rule.TenantId = DevTenantId;
            rule.CompanyId = DevCompanyId;
            db.PostingRules.Add(rule);
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
