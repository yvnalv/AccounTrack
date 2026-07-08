using Accountrack.Sales.Domain;

namespace Accountrack.Sales.Application.Abstractions;

public interface ISalesOrderRepository
{
    void Add(SalesOrder order);
    Task<SalesOrder?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SalesOrder>> ListAsync(CancellationToken ct);
    Task<SalesOrderNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(SalesOrderNumberSequence sequence);

    /// <summary>
    /// Sets the concurrency token the caller expects to still be current, so the next save fails with
    /// a concurrency conflict if the order was changed by someone else since it was loaded (ADR-0021).
    /// </summary>
    void SetExpectedVersion(SalesOrder order, byte[] expectedVersion);
}

public interface IDeliveryOrderRepository
{
    void Add(DeliveryOrder delivery);
    Task<DeliveryOrder?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DeliveryOrder>> ListAsync(CancellationToken ct);
    Task<IReadOnlyList<DeliveryOrder>> ListBySalesOrderAsync(Guid salesOrderId, CancellationToken ct);
    Task<DeliveryOrderNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(DeliveryOrderNumberSequence sequence);
}

public interface ISalesInvoiceRepository
{
    void Add(SalesInvoice invoice);
    Task<SalesInvoice?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SalesInvoice>> ListBySalesOrderAsync(Guid salesOrderId, CancellationToken ct);
    Task<IReadOnlyList<SalesInvoice>> ListAsync(CancellationToken ct);
    Task<SalesInvoiceNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(SalesInvoiceNumberSequence sequence);
}

public interface ICustomerPaymentRepository
{
    void Add(CustomerPayment payment);
    Task<CustomerPayment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CustomerPayment>> ListByCustomerAsync(Guid customerId, CancellationToken ct);
    Task<IReadOnlyList<CustomerPayment>> ListAsync(CancellationToken ct);
    Task<CustomerPaymentNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(CustomerPaymentNumberSequence sequence);
}

public interface ISalesReturnRepository
{
    void Add(SalesReturn salesReturn);
    Task<SalesReturn?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SalesReturn>> ListBySalesOrderAsync(Guid salesOrderId, CancellationToken ct);
    Task<IReadOnlyList<SalesReturn>> ListAsync(CancellationToken ct);
    Task<SalesReturnNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(SalesReturnNumberSequence sequence);
}

public interface ISalesUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
