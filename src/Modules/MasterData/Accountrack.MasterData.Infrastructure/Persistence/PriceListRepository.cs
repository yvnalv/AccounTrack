using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.MasterData.Infrastructure.Persistence;

/// <summary>Company-scoped repository for price lists and their items (ADR-0035).</summary>
public sealed class PriceListRepository : IPriceListRepository
{
    private readonly MasterDataDbContext _db;

    public PriceListRepository(MasterDataDbContext db) => _db = db;

    public void Add(PriceList list) => _db.PriceLists.Add(list);
    public void AddItem(PriceListItem item) => _db.PriceListItems.Add(item);
    public void RemoveItem(PriceListItem item) => _db.PriceListItems.Remove(item);

    public Task<PriceList?> GetAsync(Guid id, CancellationToken ct) =>
        _db.PriceLists.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<PriceList>> ListAsync(CancellationToken ct) =>
        await _db.PriceLists.OrderBy(p => p.Type).ThenBy(p => p.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<PriceList>> ListByTypeAsync(PriceListType type, CancellationToken ct) =>
        await _db.PriceLists.Where(p => p.Type == type).ToListAsync(ct);

    public Task<PriceList?> GetDefaultAsync(PriceListType type, CancellationToken ct) =>
        _db.PriceLists.FirstOrDefaultAsync(p => p.Type == type && p.IsDefault && p.IsActive, ct);

    public async Task<IReadOnlyList<PriceListItem>> GetItemsAsync(Guid priceListId, CancellationToken ct) =>
        await _db.PriceListItems.Where(i => i.PriceListId == priceListId).ToListAsync(ct);

    public Task<PriceListItem?> GetItemAsync(Guid priceListId, Guid productId, CancellationToken ct) =>
        _db.PriceListItems.FirstOrDefaultAsync(i => i.PriceListId == priceListId && i.ProductId == productId, ct);

    public async Task<IReadOnlyDictionary<Guid, int>> ItemCountsAsync(CancellationToken ct) =>
        await _db.PriceListItems
            .GroupBy(i => i.PriceListId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public void SetExpectedVersion(PriceList list, byte[] expectedVersion) =>
        _db.Entry(list).Property(e => e.RowVersion).OriginalValue = expectedVersion;
}
