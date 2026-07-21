using Accountrack.Modules.Contracts.Company;

namespace Accountrack.Api.Infrastructure;

/// <summary>
/// Repairs companies that were provisioned before company-foundation seeding existed (BR-CMP-1).
/// <para>
/// Self-serve organization sign-up originally created only a tenant + company row: no chart of
/// accounts, no fiscal periods, no posting rules, no baseline master data. Such a tenant can sign in
/// but every GL-posting action fails (goods receipt, invoicing, payments, expenses, stock movements).
/// This runs each module's <see cref="ICompanyFoundationSeeder"/> over every existing company at
/// startup; the seeders are idempotent, so already-provisioned companies are untouched and it is safe
/// to run on every boot.
/// </para>
/// </summary>
public static class CompanyFoundationBackfill
{
    public static async Task BackfillCompanyFoundationsAsync(
        this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CompanyFoundationBackfill));
        var companies = await sp.GetRequiredService<ICompanyProvisioning>().ListAllCompaniesAsync(ct);
        var seeders = sp.GetServices<ICompanyFoundationSeeder>().OrderBy(s => s.Order).ToList();

        if (companies.Count == 0 || seeders.Count == 0)
        {
            return;
        }

        var year = DateTime.UtcNow.Year;
        foreach (var company in companies)
        {
            var foundation = new CompanyFoundation(
                company.TenantId, company.CompanyId, company.FunctionalCurrency, year,
                company.FiscalYearStartMonth);

            foreach (var seeder in seeders)
            {
                try
                {
                    await seeder.SeedAsync(foundation, ct);
                }
                catch (Exception ex)
                {
                    // One company's failure must not stop the host from starting or block the others.
                    logger.LogError(ex,
                        "Company-foundation backfill failed for company {CompanyId} (tenant {TenantId}) in {Seeder}.",
                        company.CompanyId, company.TenantId, seeder.GetType().Name);
                }
            }
        }

        logger.LogInformation(
            "Company-foundation backfill checked {CompanyCount} companies with {SeederCount} seeders.",
            companies.Count, seeders.Count);
    }
}
