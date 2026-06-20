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

public interface IPurchaseInvoiceRepository
{
    void Add(PurchaseInvoice invoice);
    Task<PurchaseInvoice?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PurchaseInvoice>> ListByPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken ct);
    Task<PurchaseInvoiceNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(PurchaseInvoiceNumberSequence sequence);
}

public interface ISupplierPaymentRepository
{
    void Add(SupplierPayment payment);
    Task<SupplierPayment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SupplierPayment>> ListBySupplierAsync(Guid supplierId, CancellationToken ct);
    Task<SupplierPaymentNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(SupplierPaymentNumberSequence sequence);
}

public interface IPurchaseReturnRepository
{
    void Add(PurchaseReturn purchaseReturn);
    Task<PurchaseReturn?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PurchaseReturn>> ListByPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken ct);
    Task<PurchaseReturnNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(PurchaseReturnNumberSequence sequence);
}

public interface IPurchasingUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
