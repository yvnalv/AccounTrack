using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Accounting.Infrastructure.Persistence;

public sealed class AccountRepository : IAccountRepository
{
    private readonly AccountingDbContext _db;
    public AccountRepository(AccountingDbContext db) => _db = db;

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Account?> GetByCodeAsync(string code, CancellationToken ct) =>
        _db.Accounts.FirstOrDefaultAsync(a => a.Code == code, ct);

    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken ct) =>
        await _db.Accounts.OrderBy(a => a.Code).ToListAsync(ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        _db.Accounts.AnyAsync(a => a.Code == code, ct);

    public async Task<IReadOnlyDictionary<Guid, Account>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await _db.Accounts.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

    public void Add(Account account) => _db.Accounts.Add(account);
}

public sealed class FiscalPeriodRepository : IFiscalPeriodRepository
{
    private readonly AccountingDbContext _db;
    public FiscalPeriodRepository(AccountingDbContext db) => _db = db;

    public Task<bool> FiscalYearExistsAsync(int year, CancellationToken ct) =>
        _db.FiscalYears.AnyAsync(fy => fy.Year == year, ct);

    public void AddFiscalYear(FiscalYear fiscalYear) => _db.FiscalYears.Add(fiscalYear);

    public Task<FiscalPeriod?> GetPeriodForDateAsync(DateOnly date, CancellationToken ct) =>
        _db.FiscalPeriods.FirstOrDefaultAsync(p => p.StartDate <= date && p.EndDate >= date, ct);

    public Task<FiscalPeriod?> GetPeriodByIdAsync(Guid id, CancellationToken ct) =>
        _db.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<FiscalYear>> ListYearsWithPeriodsAsync(CancellationToken ct) =>
        await _db.FiscalYears.Include(fy => fy.Periods).OrderBy(fy => fy.Year).ToListAsync(ct);
}

public sealed class JournalRepository : IJournalRepository
{
    private readonly AccountingDbContext _db;
    public JournalRepository(AccountingDbContext db) => _db = db;

    public void Add(JournalEntry entry) => _db.JournalEntries.Add(entry);

    public Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.JournalEntries.Include(j => j.Lines).FirstOrDefaultAsync(j => j.Id == id, ct);

    public Task<JournalNumberSequence?> GetSequenceAsync(CancellationToken ct) =>
        _db.JournalNumberSequences.FirstOrDefaultAsync(ct);

    public void AddSequence(JournalNumberSequence sequence) => _db.JournalNumberSequences.Add(sequence);
}

public sealed class PostingRuleRepository : IPostingRuleRepository
{
    private readonly AccountingDbContext _db;
    public PostingRuleRepository(AccountingDbContext db) => _db = db;

    public async Task<IReadOnlyList<PostingRule>> ListAsync(CancellationToken ct) =>
        await _db.PostingRules.ToListAsync(ct);

    public Task<PostingRule?> FindAsync(string eventType, string ruleKey, PostingSelector selector, CancellationToken ct) =>
        _db.PostingRules.FirstOrDefaultAsync(r =>
            r.EventType == eventType
            && r.RuleKey == ruleKey
            && r.ProductCategoryId == selector.ProductCategoryId
            && r.WarehouseId == selector.WarehouseId
            && r.TaxCodeId == selector.TaxCodeId
            && r.BankAccountId == selector.BankAccountId,
            ct);

    public void Add(PostingRule rule) => _db.PostingRules.Add(rule);
}

public sealed class SubledgerRepository : ISubledgerRepository
{
    private readonly AccountingDbContext _db;
    public SubledgerRepository(AccountingDbContext db) => _db = db;

    public void Add(SubledgerOpenItem item) => _db.SubledgerOpenItems.Add(item);

    public Task<SubledgerOpenItem?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.SubledgerOpenItems.Include(i => i.Allocations).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<SubledgerOpenItem>> ListAsync(
        SubledgerType type, Guid? partyId, bool includeSettled, CancellationToken ct)
    {
        var query = _db.SubledgerOpenItems.Where(i => i.Type == type);

        if (partyId.HasValue)
        {
            query = query.Where(i => i.PartyId == partyId.Value);
        }

        if (!includeSettled)
        {
            query = query.Where(i => i.Status != OpenItemStatus.Settled);
        }

        return await query.OrderBy(i => i.DueDate).ToListAsync(ct);
    }
}

public sealed class AccountingReadStore : IAccountingReadStore
{
    private readonly AccountingDbContext _db;
    public AccountingReadStore(AccountingDbContext db) => _db = db;

    public async Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(
        DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        // Derived from the GL: posted (and reversed — historically posted) journal lines only.
        var query =
            from line in _db.JournalLines
            join entry in _db.JournalEntries on line.JournalEntryId equals entry.Id
            join account in _db.Accounts on line.AccountId equals account.Id
            where entry.Status != JournalStatus.Draft
                && (fromDate == null || entry.Date >= fromDate)
                && (toDate == null || entry.Date <= toDate)
            group new { line, account } by new { account.Code, account.Name, account.Type } into g
            orderby g.Key.Code
            select new TrialBalanceRow(
                g.Key.Code,
                g.Key.Name,
                g.Key.Type.ToString(),
                g.Sum(x => x.line.Debit.Amount),
                g.Sum(x => x.line.Credit.Amount),
                g.Sum(x => x.line.Debit.Amount) - g.Sum(x => x.line.Credit.Amount));

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AccountMovementRow>> GetAccountMovementsAsync(
        IReadOnlyCollection<Guid> accountIds, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        var query =
            from line in _db.JournalLines
            join entry in _db.JournalEntries on line.JournalEntryId equals entry.Id
            where accountIds.Contains(line.AccountId)
                && entry.Status != JournalStatus.Draft
                && (fromDate == null || entry.Date >= fromDate)
                && (toDate == null || entry.Date <= toDate)
            group line by line.AccountId into g
            select new AccountMovementRow(
                g.Key,
                g.Sum(x => x.Debit.Amount),
                g.Sum(x => x.Credit.Amount));

        return await query.ToListAsync(ct);
    }
}
