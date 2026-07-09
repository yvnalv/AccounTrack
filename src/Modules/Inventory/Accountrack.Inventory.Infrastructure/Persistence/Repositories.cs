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

    public async Task<IReadOnlyList<StockCostBucket>> ListForProductAsync(Guid productId, CancellationToken ct) =>
        await _db.StockCostBuckets.Where(b => b.ProductId == productId).ToListAsync(ct);

    public void Add(StockCostBucket bucket) => _db.StockCostBuckets.Add(bucket);
}

public sealed class StockCostLayerRepository : IStockCostLayerRepository
{
    private readonly InventoryDbContext _db;
    public StockCostLayerRepository(InventoryDbContext db) => _db = db;

    public void Add(StockCostLayer layer) => _db.StockCostLayers.Add(layer);

    public async Task<IReadOnlyList<StockCostLayer>> ListOpenForBucketAsync(
        Guid productId, Guid warehouseId, CancellationToken ct) =>
        await _db.StockCostLayers
            .Where(l => l.ProductId == productId && l.WarehouseId == warehouseId && l.RemainingQty > 0m)
            .OrderBy(l => l.MovementDate)
            .ThenBy(l => l.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StockCostLayer>> ListAllForBucketAsync(
        Guid productId, Guid warehouseId, CancellationToken ct) =>
        await _db.StockCostLayers
            .Where(l => l.ProductId == productId && l.WarehouseId == warehouseId)
            .OrderBy(l => l.MovementDate)
            .ThenBy(l => l.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StockCostLayer>> ListOpenAsync(CancellationToken ct) =>
        await _db.StockCostLayers.Where(l => l.RemainingQty > 0m).ToListAsync(ct);
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

    public async Task<IReadOnlyList<InventoryTransaction>> ListForProductChronologicalAsync(
        Guid productId, CancellationToken ct) =>
        await _db.InventoryTransactions
            .Where(t => t.ProductId == productId)
            .OrderBy(t => t.MovementDate)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<bool> HasTransferOnOrAfterAsync(Guid productId, DateOnly date, CancellationToken ct) =>
        await _db.InventoryTransactions.AnyAsync(
            t => t.ProductId == productId && t.MovementDate >= date
                && (t.Type == MovementType.TransferOut || t.Type == MovementType.TransferIn),
            ct);

    public async Task<DateOnly?> MaxMovementDateAsync(Guid productId, Guid warehouseId, CancellationToken ct)
    {
        var dates = _db.InventoryTransactions
            .Where(t => t.ProductId == productId && t.WarehouseId == warehouseId)
            .Select(t => (DateOnly?)t.MovementDate);
        return await dates.MaxAsync(ct);
    }
}
