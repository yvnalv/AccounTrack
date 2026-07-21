using Accountrack.MasterData.Infrastructure.Persistence;
using Accountrack.MasterData.Infrastructure.Seed;
using Accountrack.Modules.Contracts.Company;

namespace Accountrack.MasterData.Infrastructure.Provisioning;

/// <summary>
/// Gives a newly provisioned company its baseline master data: a base unit of measure, a default
/// product category, a main warehouse and the PPN 11% tax code (BR-CMP-1). Products cannot be created
/// without a unit, and no stock can move without a warehouse.
/// </summary>
public sealed class MasterDataCompanyFoundationSeeder : ICompanyFoundationSeeder
{
    private readonly MasterDataDbContext _db;

    public MasterDataCompanyFoundationSeeder(MasterDataDbContext db) => _db = db;

    public int Order => 200;

    public Task SeedAsync(CompanyFoundation company, CancellationToken ct) =>
        MasterDataSeeder.SeedForCompanyAsync(_db, company.TenantId, company.CompanyId, ct);
}
