using Accountrack.CompanyManagement.Domain;
using Accountrack.CompanyManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.CompanyManagement.Infrastructure.Seed;

/// <summary>
/// Seeds a development tenant + company so the platform is usable out of the box. The ids match
/// the well-known values the Identity dev seeder grants its admin user, keeping the two modules
/// consistent until real onboarding/provisioning exists.
/// </summary>
public static class CompanyDataSeeder
{
    // Must match Accountrack.Identity.Infrastructure.Seed.IdentityDataSeeder.DevTenantId/DevCompanyId.
    public static readonly Guid DevTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DevCompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task SeedAsync(CompanyDbContext db, CancellationToken ct = default)
    {
        // Tenant is the tenancy root (no tenant filter) — a normal query works without context.
        if (!await db.Tenants.AnyAsync(t => t.Id == DevTenantId, ct))
        {
            db.Tenants.Add(Tenant.CreateWithId(DevTenantId, "Demo Tenant"));
        }

        // Company is tenant-scoped; bypass the tenant filter since no tenant context exists at seed.
        var companyExists = await db.Companies
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Id == DevCompanyId && !c.IsDeleted, ct);

        if (!companyExists)
        {
            db.Companies.Add(Company.CreateWithId(
                DevCompanyId,
                DevTenantId,
                code: "MAIN",
                name: "Main Company",
                functionalCurrency: "IDR",
                fiscalYearStartMonth: 1,
                timeZone: "Asia/Jakarta",
                isVatRegistered: true)); // demo company is PKP so VAT flows/reports stay meaningful
        }

        await db.SaveChangesAsync(ct);
    }
}
