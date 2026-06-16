using Accountrack.Sales.Domain;

namespace Accountrack.Sales.Application.Abstractions;

public interface ISalesOrderRepository
{
    void Add(SalesOrder order);
    Task<SalesOrder?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SalesOrder>> ListAsync(CancellationToken ct);
    Task<SalesOrderNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(SalesOrderNumberSequence sequence);
}

public interface IDeliveryOrderRepository
{
    void Add(DeliveryOrder delivery);
    Task<DeliveryOrder?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DeliveryOrder>> ListBySalesOrderAsync(Guid salesOrderId, CancellationToken ct);
    Task<DeliveryOrderNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(DeliveryOrderNumberSequence sequence);
}

public interface ISalesUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
