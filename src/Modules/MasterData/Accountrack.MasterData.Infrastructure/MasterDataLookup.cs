using Accountrack.MasterData.Domain;
using Accountrack.MasterData.Infrastructure.Persistence;
using Accountrack.Modules.Contracts.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.MasterData.Infrastructure;

/// <summary>Implements the public <see cref="IMasterDataLookup"/> contract for reference validation.</summary>
public sealed class MasterDataLookup : IMasterDataLookup
{
    private readonly MasterDataDbContext _db;

    public MasterDataLookup(MasterDataDbContext db) => _db = db;

    public Task<bool> SupplierExistsAsync(Guid supplierId, CancellationToken ct) =>
        _db.Set<Supplier>().AnyAsync(s => s.Id == supplierId, ct);

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken ct) =>
        _db.Set<Customer>().AnyAsync(c => c.Id == customerId, ct);

    public Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct) =>
        _db.Set<Product>().AnyAsync(p => p.Id == productId, ct);

    public Task<bool> WarehouseExistsAsync(Guid warehouseId, CancellationToken ct) =>
        _db.Set<Warehouse>().AnyAsync(w => w.Id == warehouseId, ct);
}
