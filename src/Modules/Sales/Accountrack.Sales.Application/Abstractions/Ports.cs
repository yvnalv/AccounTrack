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

public interface ISalesUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
