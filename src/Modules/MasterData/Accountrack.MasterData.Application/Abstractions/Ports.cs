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
}

public interface IMasterDataUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
