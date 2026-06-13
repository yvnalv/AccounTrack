using Accountrack.Accounting.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Accounting.Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Account?> GetByCodeAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<Account>> ListAsync(CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, Account>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    void Add(Account account);
}

public interface IFiscalPeriodRepository
{
    Task<bool> FiscalYearExistsAsync(int year, CancellationToken ct);
    void AddFiscalYear(FiscalYear fiscalYear);
    Task<FiscalPeriod?> GetPeriodForDateAsync(DateOnly date, CancellationToken ct);
    Task<FiscalPeriod?> GetPeriodByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<FiscalYear>> ListYearsWithPeriodsAsync(CancellationToken ct);
}

public interface IJournalRepository
{
    void Add(JournalEntry entry);
    Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<JournalNumberSequence?> GetSequenceAsync(CancellationToken ct);
    void AddSequence(JournalNumberSequence sequence);
}

public interface IAccountingUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}

/// <summary>
/// Posts a balanced draft journal: validates the fiscal period is open and the accounts are
/// postable, assigns the entry number, and adds it to the unit of work (caller saves). Shared by
/// the manual-posting endpoint, reversals, and (later) event-driven posting from other modules.
/// </summary>
public interface IJournalPoster
{
    Task<Result<Guid>> PostAsync(JournalEntry draft, CancellationToken ct);
}

public sealed record TrialBalanceRow(
    string AccountCode, string AccountName, string AccountType, decimal Debit, decimal Credit, decimal Balance);

public interface IAccountingReadStore
{
    Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct);
}
