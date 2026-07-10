using Accountrack.MasterData.Domain;
using Accountrack.MasterData.Infrastructure.Persistence;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.SharedKernel.Inventory;
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

    public async Task<CostingMethod> GetCostingMethodAsync(Guid productId, CancellationToken ct) =>
        await _db.Set<Product>().Where(p => p.Id == productId)
            .Select(p => p.CostingMethod).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> ResolveNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var map = new Dictionary<Guid, string>();
        if (ids.Count == 0)
        {
            return map;
        }

        async Task Add(IQueryable<KeyValuePair<Guid, string>> q)
        {
            foreach (var kv in await q.ToListAsync(ct))
            {
                map[kv.Key] = kv.Value;
            }
        }

        await Add(_db.Set<Customer>().Where(e => ids.Contains(e.Id)).Select(e => new KeyValuePair<Guid, string>(e.Id, e.Name)));
        await Add(_db.Set<Supplier>().Where(e => ids.Contains(e.Id)).Select(e => new KeyValuePair<Guid, string>(e.Id, e.Name)));
        await Add(_db.Set<Product>().Where(e => ids.Contains(e.Id)).Select(e => new KeyValuePair<Guid, string>(e.Id, e.Name)));
        await Add(_db.Set<Warehouse>().Where(e => ids.Contains(e.Id)).Select(e => new KeyValuePair<Guid, string>(e.Id, e.Name)));
        return map;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ResolveProductCategoryNamesAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var pairs = await (
            from p in _db.Set<Product>()
            where productIds.Contains(p.Id) && p.CategoryId != null
            join c in _db.Set<ProductCategory>() on p.CategoryId equals c.Id
            select new KeyValuePair<Guid, string>(p.Id, c.Name)).ToListAsync(ct);

        return pairs.ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
