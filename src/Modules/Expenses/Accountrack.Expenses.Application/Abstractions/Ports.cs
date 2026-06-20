using Accountrack.Expenses.Domain;

namespace Accountrack.Expenses.Application.Abstractions;

public interface IExpenseCategoryRepository
{
    void Add(ExpenseCategory category);
    Task<ExpenseCategory?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<ExpenseCategory>> ListAsync(CancellationToken ct);
}

public interface IExpenseVoucherRepository
{
    void Add(ExpenseVoucher voucher);
    Task<ExpenseVoucher?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ExpenseVoucher>> ListAsync(CancellationToken ct);
    Task<ExpenseVoucherNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(ExpenseVoucherNumberSequence sequence);
}

public interface IExpensesUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
