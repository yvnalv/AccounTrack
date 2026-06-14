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

public interface IGoodsReceiptRepository
{
    void Add(GoodsReceipt receipt);
    Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<GoodsReceipt>> ListByPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken ct);
    Task<GoodsReceiptNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(GoodsReceiptNumberSequence sequence);
}

public interface IPurchasingUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
