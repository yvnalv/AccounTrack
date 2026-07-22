using Accountrack.Accounting.Domain;
using Accountrack.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Accounting.Infrastructure.Seed;

/// <summary>
/// Seeds the accounting foundation a company cannot operate without: a default Indonesian-SMB chart of
/// accounts, a fiscal year with its periods, and the default posting rules. Account codes match the
/// defaults in docs/POSTING_RULES.md §2.
/// <para>
/// This runs for <b>every</b> company — the dev seed and every self-registered organization alike
/// (BR-CMP-1). Without it no GL-posting action can succeed (goods receipt, invoicing, payments,
/// expenses, stock adjustments), because posting-rule resolution and the open-period check both fail.
/// Every step is idempotent, so it is safe to re-run as a backfill over existing companies.
/// </para>
/// </summary>
public static class AccountingDataSeeder
{
    private static readonly Guid DevTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DevCompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Seeds the dev company (startup dev seed).</summary>
    public static Task SeedAsync(AccountingDbContext db, int currentYear, CancellationToken ct = default) =>
        SeedForCompanyAsync(db, DevTenantId, DevCompanyId, currentYear, fiscalYearStartMonth: 1, ct);

    /// <summary>
    /// Seeds the foundation for an arbitrary company. Used when a new organization/company is
    /// provisioned and by the startup backfill. Idempotent per step.
    /// </summary>
    public static async Task SeedForCompanyAsync(
        AccountingDbContext db, Guid tenantId, Guid companyId, int year, int fiscalYearStartMonth = 1,
        CancellationToken ct = default)
    {
        await SeedChartAsync(db, tenantId, companyId, ct);
        await SeedFiscalYearAsync(db, tenantId, companyId, year, fiscalYearStartMonth, ct);
        await SeedPostingRulesAsync(db, tenantId, companyId, ct);
    }

    private static async Task SeedChartAsync(AccountingDbContext db, Guid tenantId, Guid companyId, CancellationToken ct)
    {
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
            // Financing (ADR-0040).
            ("2500", "Bank Loan Payable", AccountType.Liability, false, ControlType.None),
            ("2600", "Dividends Payable", AccountType.Liability, false, ControlType.None),
            // Equity — both sole-proprietor and PT structures (ADR-0040).
            ("3000", "Owner's Capital (Modal Pemilik)", AccountType.Equity, false, ControlType.None),
            ("3100", "Additional Paid-in Capital (Agio Saham)", AccountType.Equity, false, ControlType.None),
            ("3200", "Owner's Drawings (Prive)", AccountType.Equity, false, ControlType.None),
            ("3300", "Share Capital (Modal Saham)", AccountType.Equity, false, ControlType.None),
            ("3400", "Dividends Declared", AccountType.Equity, false, ControlType.None),
            ("3900", "Retained Earnings", AccountType.Equity, false, ControlType.None),
            ("3950", "Opening Balance Equity", AccountType.Equity, false, ControlType.None),
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

        // Per-code idempotency (not all-or-nothing): add only the codes a company is missing, so new
        // default accounts (e.g. equity/loan — ADR-0040) backfill onto companies seeded before they existed.
        var existingCodes = await db.Accounts.IgnoreQueryFilters()
            .Where(a => a.CompanyId == companyId && !a.IsDeleted)
            .Select(a => a.Code)
            .ToListAsync(ct);
        var existing = existingCodes.ToHashSet();

        foreach (var a in chart)
        {
            if (existing.Contains(a.Code))
            {
                continue;
            }

            var account = Account.Create(a.Code, a.Name, a.Type, isControlAccount: a.Control, controlType: a.Ct, isSystem: true);
            account.TenantId = tenantId;
            account.CompanyId = companyId;
            db.Accounts.Add(account);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds the company-wide default posting rules (docs/POSTING_RULES.md §2): each required
    /// <see cref="PostingRuleKeys"/> maps to its default seeded account. CashBank is intentionally
    /// not seeded as a single default — it is resolved per chosen bank/cash account via a selector.
    /// </summary>
    private static async Task SeedPostingRulesAsync(AccountingDbContext db, Guid tenantId, Guid companyId, CancellationToken ct)
    {
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
            // Equity & financing defaults for the guided Cash & Bank flows (ADR-0040).
            (PostingRuleKeys.OwnerCapital, "3000"),
            (PostingRuleKeys.OwnerDrawings, "3200"),
            (PostingRuleKeys.ShareCapital, "3300"),
            (PostingRuleKeys.LoanPayable, "2500"),
            (PostingRuleKeys.OpeningBalanceEquity, "3950"),
            // Expense categories (ADR-0030). Keys match Expenses module's seeded categories.
            ("EXPENSE.ELECTRICITY", "6000"),
            ("EXPENSE.TRANSPORT", "6100"),
            ("EXPENSE.RENT", "6200"),
            ("EXPENSE.SUPPLIES", "6300"),
            ("EXPENSE.SALARIES", "6400"),
            ("EXPENSE.OTHER", "6900"),
        };

        var accountsByCode = await db.Accounts.IgnoreQueryFilters()
            .Where(a => a.CompanyId == companyId && !a.IsDeleted)
            .ToDictionaryAsync(a => a.Code, a => a.Id, ct);

        // Per-key idempotency: add only the company-wide default rules (AnyEvent, no selectors) that are
        // missing, so new default keys (equity/loan — ADR-0040) backfill onto already-configured companies
        // without duplicating or disturbing any rule the user has customized.
        var existingDefaultKeys = await db.PostingRules.IgnoreQueryFilters()
            .Where(r => r.CompanyId == companyId && !r.IsDeleted
                && r.EventType == PostingRule.AnyEvent
                && r.ProductCategoryId == null && r.WarehouseId == null
                && r.TaxCodeId == null && r.BankAccountId == null)
            .Select(r => r.RuleKey)
            .ToListAsync(ct);
        var configured = existingDefaultKeys.ToHashSet();

        foreach (var (key, code) in defaults)
        {
            if (configured.Contains(key) || !accountsByCode.TryGetValue(code, out var accountId))
            {
                continue;
            }

            var rule = PostingRule.CreateDefault(key, accountId);
            rule.TenantId = tenantId;
            rule.CompanyId = companyId;
            db.PostingRules.Add(rule);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedFiscalYearAsync(
        AccountingDbContext db, Guid tenantId, Guid companyId, int year, int startMonth, CancellationToken ct)
    {
        var hasYear = await db.FiscalYears.IgnoreQueryFilters()
            .AnyAsync(fy => fy.CompanyId == companyId && fy.Year == year && !fy.IsDeleted, ct);
        if (hasYear)
        {
            return;
        }

        var fiscalYear = FiscalYear.Create(year, startMonth);
        fiscalYear.TenantId = tenantId;
        fiscalYear.CompanyId = companyId;
        foreach (var period in fiscalYear.Periods)
        {
            period.TenantId = tenantId;
            period.CompanyId = companyId;
        }

        db.FiscalYears.Add(fiscalYear);
        await db.SaveChangesAsync(ct);
    }
}
