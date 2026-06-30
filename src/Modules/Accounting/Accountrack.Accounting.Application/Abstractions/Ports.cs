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

    /// <summary>
    /// Sets the concurrency token the caller expects to still be current, so the next save fails with
    /// a concurrency conflict if the account was changed by someone else since it was loaded (ADR-0021).
    /// </summary>
    void SetExpectedVersion(Account account, byte[] expectedVersion);
}

public interface IFiscalPeriodRepository
{
    Task<bool> FiscalYearExistsAsync(int year, CancellationToken ct);
    void AddFiscalYear(FiscalYear fiscalYear);
    Task<FiscalPeriod?> GetPeriodForDateAsync(DateOnly date, CancellationToken ct);
    Task<FiscalPeriod?> GetPeriodByIdAsync(Guid id, CancellationToken ct);
    Task<FiscalYear?> GetFiscalYearByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<FiscalYear>> ListYearsWithPeriodsAsync(CancellationToken ct);

    // Period-close balance snapshots (ADR-0022).
    void AddPeriodBalance(PeriodBalance balance);
    Task ClearPeriodBalancesAsync(Guid fiscalPeriodId, CancellationToken ct);
    Task<IReadOnlyList<PeriodBalance>> GetPeriodBalancesAsync(Guid fiscalPeriodId, CancellationToken ct);
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

/// <summary>Period debit/credit movement on a single account, derived from posted journal lines.</summary>
public sealed record AccountMovementRow(Guid AccountId, decimal Debit, decimal Credit);

/// <summary>A single posted journal line with its account and source entry, for the GL detail report.</summary>
public sealed record GeneralLedgerLineRow(
    Guid AccountId, string AccountCode, string AccountName, string AccountType,
    DateOnly Date, string EntryNo, string Source, Guid? SourceDocumentId, string? Description,
    decimal Debit, decimal Credit);

public interface IAccountingReadStore
{
    Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct);

    /// <summary>Summed debit/credit per account over the period for the given accounts (posted lines only).</summary>
    Task<IReadOnlyList<AccountMovementRow>> GetAccountMovementsAsync(
        IReadOnlyCollection<Guid> accountIds, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct);

    /// <summary>Posted journal lines (optionally for one account) over a period, ordered by account then date.</summary>
    Task<IReadOnlyList<GeneralLedgerLineRow>> GetGeneralLedgerAsync(
        Guid? accountId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct);
}

public interface IPostingRuleRepository
{
    /// <summary>All posting rules for the active company (small per-company set; resolved in memory).</summary>
    Task<IReadOnlyList<PostingRule>> ListAsync(CancellationToken ct);
    Task<PostingRule?> FindAsync(string eventType, string ruleKey, PostingSelector selector, CancellationToken ct);
    void Add(PostingRule rule);
}

/// <summary>
/// Account-determination engine (ADR-0024, docs/POSTING_RULES.md): maps a business event + purpose
/// (+ optional selectors) to a GL account, most-specific rule wins, company default as fallback.
/// Used by event-driven posting so accounts are configuration, never hardcoded.
/// </summary>
public interface IPostingRuleResolver
{
    Task<Result<Guid>> ResolveAsync(string eventType, string ruleKey, PostingSelector selector, CancellationToken ct);
}

public interface ISubledgerRepository
{
    void Add(SubledgerOpenItem item);
    Task<SubledgerOpenItem?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SubledgerOpenItem>> ListAsync(
        SubledgerType type, Guid? partyId, bool includeSettled, CancellationToken ct);
}

/// <summary>
/// AR/AP subledger operations (ADR-0011): create open items from documents (or opening balances)
/// and allocate payments to them. Reused by the future Sales/Purchasing invoice + payment posting
/// so the subledger always moves in step with the GL control account.
/// </summary>
public interface ISubledgerService
{
    Task<Result<Guid>> OpenItemAsync(
        SubledgerType type, Guid partyId, JournalSource sourceType, Guid? sourceDocumentId,
        string documentNo, DateOnly documentDate, DateOnly dueDate, decimal amount, string currency,
        CancellationToken ct);

    Task<Result<Guid>> AllocateAsync(
        Guid openItemId, string paymentReference, DateOnly date, decimal amount, Guid? paymentDocumentId,
        CancellationToken ct);

    /// <summary>The open item's remaining (unsettled) balance — 0 once fully paid.</summary>
    Task<Result<decimal>> GetOutstandingAsync(Guid openItemId, CancellationToken ct);
}
