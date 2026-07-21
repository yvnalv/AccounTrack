using Accountrack.MasterData.Domain;
using Accountrack.MasterData.Infrastructure.Persistence;
using Accountrack.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.MasterData.Infrastructure.Seed;

/// <summary>
/// Seeds the minimal master data a company cannot operate without: a base unit, a default product
/// category, a main warehouse, and the PPN 11% tax code.
/// <para>
/// This runs for <b>every</b> company — the dev seed and every self-registered organization alike
/// (BR-CMP-1) — because product/document creation needs a unit, and stock movements need a warehouse.
/// Idempotent, so it is safe to re-run as a backfill over existing companies.
/// </para>
/// </summary>
public static class MasterDataSeeder
{
    private static readonly Guid DevTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DevCompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Seeds the dev company (startup dev seed).</summary>
    public static Task SeedAsync(MasterDataDbContext db, CancellationToken ct = default) =>
        SeedForCompanyAsync(db, DevTenantId, DevCompanyId, ct);

    /// <summary>
    /// Seeds the foundation for an arbitrary company. Used when a new organization/company is
    /// provisioned and by the startup backfill.
    /// </summary>
    public static async Task SeedForCompanyAsync(
        MasterDataDbContext db, Guid tenantId, Guid companyId, CancellationToken ct = default)
    {
        var seeded = await db.UnitsOfMeasure.IgnoreQueryFilters()
            .AnyAsync(u => u.CompanyId == companyId && !u.IsDeleted, ct);
        if (seeded)
        {
            return;
        }

        var pcs = UnitOfMeasure.Create("PCS", "Piece");
        var category = ProductCategory.Create("GENERAL", "General");
        var warehouse = Warehouse.Create("MAIN-WH", "Main Warehouse");
        var ppn = TaxCode.Create("PPN11", "PPN 11%", 0.11m);

        foreach (var e in new TenantOwnedEntity[] { pcs, category, warehouse, ppn })
        {
            e.TenantId = tenantId;
            e.CompanyId = companyId;
        }

        db.UnitsOfMeasure.Add(pcs);
        db.ProductCategories.Add(category);
        db.Warehouses.Add(warehouse);
        db.TaxCodes.Add(ppn);

        await db.SaveChangesAsync(ct);
    }
}
