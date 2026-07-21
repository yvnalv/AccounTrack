using Accountrack.Expenses.Domain;
using Accountrack.Expenses.Infrastructure.Persistence;
using Accountrack.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Expenses.Infrastructure.Seed;

/// <summary>
/// Seeds default expense categories for the dev company. Each category's posting-rule key matches a
/// rule seeded by the Accounting module (AccountingDataSeeder), so expenses resolve to their GL
/// account out of the box. Ids match the Company/Identity/Accounting dev seed.
/// </summary>
public static class ExpensesSeeder
{
    private static readonly Guid DevTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DevCompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Default categories: (code, name, posting-rule key). Keys are shared with Accounting seed.</summary>
    public static readonly (string Code, string Name, string RuleKey)[] DefaultCategories =
    {
        ("ELECTRICITY", "Electricity & Utilities", "EXPENSE.ELECTRICITY"),
        ("TRANSPORT", "Transportation", "EXPENSE.TRANSPORT"),
        ("RENT", "Rent", "EXPENSE.RENT"),
        ("SUPPLIES", "Office Supplies", "EXPENSE.SUPPLIES"),
        ("SALARIES", "Salaries & Wages", "EXPENSE.SALARIES"),
        ("OTHER", "Other Operating Expense", "EXPENSE.OTHER"),
    };

    /// <summary>Seeds the dev company (startup dev seed).</summary>
    public static Task SeedAsync(ExpensesDbContext db, CancellationToken ct = default) =>
        SeedForCompanyAsync(db, DevTenantId, DevCompanyId, ct);

    /// <summary>
    /// Seeds the default categories for an arbitrary company. Used when a new organization/company is
    /// provisioned and by the startup backfill (BR-CMP-1). Idempotent.
    /// </summary>
    public static async Task SeedForCompanyAsync(
        ExpensesDbContext db, Guid tenantId, Guid companyId, CancellationToken ct = default)
    {
        var seeded = await db.ExpenseCategories.IgnoreQueryFilters()
            .AnyAsync(c => c.CompanyId == companyId && !c.IsDeleted, ct);
        if (seeded)
        {
            return;
        }

        foreach (var (code, name, ruleKey) in DefaultCategories)
        {
            var category = ExpenseCategory.Create(code, name, ruleKey);
            category.TenantId = tenantId;
            category.CompanyId = companyId;
            db.ExpenseCategories.Add(category);
        }

        await db.SaveChangesAsync(ct);
    }
}
