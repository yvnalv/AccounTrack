using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Accounting.Application.Services;

/// <summary>
/// Default <see cref="IJournalPoster"/>: validates the period is open and accounts are postable,
/// assigns the entry number from the per-company sequence, and posts the entry (ADR-0009/0010).
/// Does not save — the calling use case owns the unit of work.
/// </summary>
public sealed class JournalPostingService : IJournalPoster
{
    private readonly IFiscalPeriodRepository _periods;
    private readonly IAccountRepository _accounts;
    private readonly IJournalRepository _journals;
    private readonly IClock _clock;

    public JournalPostingService(
        IFiscalPeriodRepository periods,
        IAccountRepository accounts,
        IJournalRepository journals,
        IClock clock)
    {
        _periods = periods;
        _accounts = accounts;
        _journals = journals;
        _clock = clock;
    }

    public async Task<Result<Guid>> PostAsync(JournalEntry draft, CancellationToken ct)
    {
        if (draft.Lines.Count < 2)
        {
            return AccountingErrors.TooFewLines;
        }

        if (!draft.IsBalanced)
        {
            return AccountingErrors.Unbalanced;
        }

        var period = await _periods.GetPeriodForDateAsync(draft.Date, ct);
        if (period is null)
        {
            return AccountingErrors.NoOpenPeriod;
        }

        if (!period.IsOpen)
        {
            return AccountingErrors.PeriodClosed;
        }

        var accountIds = draft.Lines.Select(l => l.AccountId).Distinct().ToArray();
        var accounts = await _accounts.GetByIdsAsync(accountIds, ct);
        foreach (var id in accountIds)
        {
            if (!accounts.TryGetValue(id, out var account))
            {
                return AccountingErrors.AccountNotFound;
            }

            if (!account.IsActive || !account.AllowPosting)
            {
                return AccountingErrors.AccountNotPostable(account.Code);
            }
        }

        var sequence = await _journals.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new JournalNumberSequence();
            _journals.AddSequence(sequence);
        }

        var entryNo = sequence.Take(draft.Date);
        draft.Post(entryNo, period.Id, _clock.UtcNow);
        _journals.Add(draft);

        return draft.Id;
    }
}
