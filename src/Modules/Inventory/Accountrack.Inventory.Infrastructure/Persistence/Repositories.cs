using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Inventory.Infrastructure.Persistence;

public sealed class StockBucketRepository : IStockBucketRepository
{
    private readonly InventoryDbContext _db;
    public StockBucketRepository(InventoryDbContext db) => _db = db;

    public Task<StockCostBucket?> GetAsync(Guid productId, Guid warehouseId, CancellationToken ct) =>
        _db.StockCostBuckets.FirstOrDefaultAsync(b => b.ProductId == productId && b.WarehouseId == warehouseId, ct);

    public async Task<IReadOnlyList<StockCostBucket>> ListAsync(CancellationToken ct) =>
        await _db.StockCostBuckets.OrderBy(b => b.WarehouseId).ToListAsync(ct);

    public void Add(StockCostBucket bucket) => _db.StockCostBuckets.Add(bucket);
}

public sealed class InventoryTransactionRepository : IInventoryTransactionRepository
{
    private readonly InventoryDbContext _db;
    public InventoryTransactionRepository(InventoryDbContext db) => _db = db;

    public void Add(InventoryTransaction transaction) => _db.InventoryTransactions.Add(transaction);

    public async Task<IReadOnlyList<InventoryTransaction>> ListAsync(Guid productId, Guid? warehouseId, CancellationToken ct)
    {
        var query = _db.InventoryTransactions.Where(t => t.ProductId == productId);
        if (warehouseId is { } wid)
        {
            query = query.Where(t => t.WarehouseId == wid);
        }

        return await query
            .OrderByDescending(t => t.MovementDate)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InventoryTransaction>> ListForBucketChronologicalAsync(
        Guid productId, Guid warehouseId, CancellationToken ct) =>
        await _db.InventoryTransactions
            .Where(t => t.ProductId == productId && t.WarehouseId == warehouseId)
            .OrderBy(t => t.MovementDate)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<DateOnly?> MaxMovementDateAsync(Guid productId, Guid warehouseId, CancellationToken ct)
    {
        var dates = _db.InventoryTransactions
            .Where(t => t.ProductId == productId && t.WarehouseId == warehouseId)
            .Select(t => (DateOnly?)t.MovementDate);
        return await dates.MaxAsync(ct);
    }
}
