using Accountrack.MasterData.Domain;
using Accountrack.MasterData.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.MasterData.Infrastructure.Seed;

/// <summary>
/// Seeds minimal master data for the dev company (a base unit, a default category, a main
/// warehouse, and the PPN 11% tax code) so transactional modules have something to work with.
/// Ids match the Company/Identity/Accounting dev seed.
/// </summary>
public static class MasterDataSeeder
{
    private static readonly Guid DevTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DevCompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task SeedAsync(MasterDataDbContext db, CancellationToken ct = default)
    {
        var seeded = await db.UnitsOfMeasure.IgnoreQueryFilters()
            .AnyAsync(u => u.CompanyId == DevCompanyId && !u.IsDeleted, ct);
        if (seeded)
        {
            return;
        }

        var pcs = UnitOfMeasure.Create("PCS", "Piece");
        var category = ProductCategory.Create("GENERAL", "General");
        var warehouse = Warehouse.Create("MAIN-WH", "Main Warehouse");
        var ppn = TaxCode.Create("PPN11", "PPN 11%", 0.11m);

        Stamp(pcs); Stamp(category); Stamp(warehouse); Stamp(ppn);

        db.UnitsOfMeasure.Add(pcs);
        db.ProductCategories.Add(category);
        db.Warehouses.Add(warehouse);
        db.TaxCodes.Add(ppn);

        await db.SaveChangesAsync(ct);
    }

    private static void Stamp(Accountrack.SharedKernel.Domain.TenantOwnedEntity e)
    {
        e.TenantId = DevTenantId;
        e.CompanyId = DevCompanyId;
    }
}
