using Accountrack.Purchasing.Domain;

namespace Accountrack.Purchasing.Application.Abstractions;

public interface IPurchaseOrderRepository
{
    void Add(PurchaseOrder order);
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken ct);
    Task<PurchaseOrderNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(PurchaseOrderNumberSequence sequence);
}

public interface IPurchasingUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
