using Accountrack.SharedKernel.Domain;

namespace Accountrack.MasterData.Application.Abstractions;

/// <summary>
/// Generic repository for coded master-data aggregates, keeping per-aggregate use cases tiny.
/// Scoped to the active company by the global query filter.
/// </summary>
public interface ICodedRepository<T> where T : Entity, IHasCode
{
    void Add(T entity);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Sets the concurrency token the caller expects to still be current, so the next save fails with
    /// a concurrency conflict if the record was changed by someone else since it was loaded (ADR-0021).
    /// </summary>
    void SetExpectedVersion(T entity, byte[] expectedVersion);
}

/// <summary>Repository for price lists and their items (ADR-0035), company-scoped by the query filter.</summary>
public interface IPriceListRepository
{
    void Add(Domain.PriceList list);
    void AddItem(Domain.PriceListItem item);
    void RemoveItem(Domain.PriceListItem item);

    Task<Domain.PriceList?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Domain.PriceList>> ListAsync(CancellationToken ct);
    Task<IReadOnlyList<Domain.PriceList>> ListByTypeAsync(Domain.PriceListType type, CancellationToken ct);
    Task<Domain.PriceList?> GetDefaultAsync(Domain.PriceListType type, CancellationToken ct);

    Task<IReadOnlyList<Domain.PriceListItem>> GetItemsAsync(Guid priceListId, CancellationToken ct);
    Task<Domain.PriceListItem?> GetItemAsync(Guid priceListId, Guid productId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, int>> ItemCountsAsync(CancellationToken ct);

    void SetExpectedVersion(Domain.PriceList list, byte[] expectedVersion);
}

public interface IMasterDataUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
