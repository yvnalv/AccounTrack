using Accountrack.Expenses.Domain;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Expenses.Application.Features;

/// <summary>Document type used when submitting an expense voucher to the approval engine.</summary>
public static class ExpenseDocumentTypes
{
    public const string ExpenseVoucher = "ExpenseVoucher";
}

/// <summary>
/// Posts an expense voucher's GL effect: Dr Expense per resolved account (+ Dr VAT Input where
/// creditable) / Cr Cash-Bank (paid) or Cr Accounts Payable + an AP open item (on account). Shared by
/// the create handler (auto-approved vouchers) and the approval consumer (post on approval), so the
/// posting rules live in exactly one place. Save-less — the caller owns the cross-module transaction.
/// </summary>
public interface IExpenseVoucherPoster
{
    Task<Result> PostAsync(ExpenseVoucher voucher, CancellationToken ct);

    /// <summary>
    /// Reverses a posted voucher (BR-EXP-4): posts a mirror journal (debits ↔ credits) dated
    /// <paramref name="reversalDate"/>, settles the AP open item for an on-account voucher (only when
    /// still fully unpaid), and moves the voucher to Reversed. The original journal is left intact.
    /// Save-less — the caller owns the cross-module transaction.
    /// </summary>
    Task<Result> ReverseAsync(ExpenseVoucher voucher, DateOnly reversalDate, string? reason, CancellationToken ct);
}

public sealed class ExpenseVoucherPoster : IExpenseVoucherPoster
{
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;
    private readonly ISubledgerPosting _subledger;

    public ExpenseVoucherPoster(
        IGeneralLedgerPoster ledger, IPostingAccountResolver accounts, ISubledgerPosting subledger)
    {
        _ledger = ledger;
        _accounts = accounts;
        _subledger = subledger;
    }

    public async Task<Result> PostAsync(ExpenseVoucher voucher, CancellationToken ct)
    {
        var built = await BuildLinesAsync(voucher, ct);
        if (built.IsFailure)
        {
            return built.Error;
        }

        var posting = new LedgerPostingRequest(
            voucher.ExpenseDate, LedgerSource.Expense, voucher.Id, $"Expense voucher {voucher.Number}", built.Value);

        var journal = await _ledger.PostAsync(posting, ct);
        if (journal.IsFailure)
        {
            return journal.Error;
        }

        voucher.MarkPosted(journal.Value);

        if (voucher.IsOnAccount)
        {
            var openItem = await _subledger.OpenPayableAsync(
                voucher.SupplierId!.Value, voucher.Id, voucher.Number, voucher.ExpenseDate, voucher.DueDate!.Value,
                voucher.GrandTotal, ct);
            if (openItem.IsFailure)
            {
                return openItem.Error;
            }

            voucher.SetApOpenItem(openItem.Value);
        }

        return Result.Success();
    }

    public async Task<Result> ReverseAsync(ExpenseVoucher voucher, DateOnly reversalDate, string? reason, CancellationToken ct)
    {
        // A reversal must not silently undo money that has already moved: block if the payable was
        // (partly) paid — the caller unwinds the supplier payment first.
        if (voucher.IsOnAccount)
        {
            var outstanding = await _subledger.GetOutstandingAsync(voucher.ApOpenItemId!.Value, ct);
            if (outstanding.IsFailure)
            {
                return outstanding.Error;
            }

            if (outstanding.Value != voucher.GrandTotal)
            {
                return ExpenseErrors.ReversalHasPayments;
            }
        }

        var built = await BuildLinesAsync(voucher, ct);
        if (built.IsFailure)
        {
            return built.Error;
        }

        // Mirror every line (debit ↔ credit) so the reversing journal exactly offsets the original.
        var mirrored = built.Value
            .Select(l => new LedgerLine(l.AccountId, l.Credit, l.Debit, $"Reversal: {l.Description}", l.SubledgerPartyId))
            .ToList();

        var description = string.IsNullOrWhiteSpace(reason)
            ? $"Reversal of expense voucher {voucher.Number}"
            : $"Reversal of expense voucher {voucher.Number} — {reason.Trim()}";

        var posting = new LedgerPostingRequest(reversalDate, LedgerSource.Expense, voucher.Id, description, mirrored);

        var journal = await _ledger.PostAsync(posting, ct);
        if (journal.IsFailure)
        {
            return journal.Error;
        }

        // Settle the (fully unpaid) payable so it no longer shows as outstanding in AP.
        if (voucher.IsOnAccount)
        {
            var settled = await _subledger.AllocateAsync(
                voucher.ApOpenItemId!.Value, $"REV {voucher.Number}", reversalDate, voucher.GrandTotal, voucher.Id, ct);
            if (settled.IsFailure)
            {
                return settled.Error;
            }
        }

        voucher.MarkReversed(journal.Value);
        return Result.Success();
    }

    /// <summary>
    /// Builds the balanced journal lines for a voucher: Dr Expense per resolved account (net) +
    /// Dr VAT Input (creditable tax) / Cr Cash-Bank (paid) or Cr AP with the supplier (on account).
    /// Shared by posting and reversal so the account determination lives in exactly one place.
    /// </summary>
    private async Task<Result<List<LedgerLine>>> BuildLinesAsync(ExpenseVoucher voucher, CancellationToken ct)
    {
        // Resolve each line's expense account via the posting-rule engine and accumulate the debit per
        // account (so a multi-line voucher posts one Dr per distinct expense account).
        var expenseDebits = new Dictionary<Guid, decimal>();
        foreach (var line in voucher.Lines)
        {
            var account = await _accounts.ResolveAsync("Expense", line.ExpenseRuleKey, PostingSelector.None, ct);
            if (account.IsFailure)
            {
                return account.Error;
            }

            expenseDebits[account.Value] = expenseDebits.GetValueOrDefault(account.Value) + line.Amount;
        }

        var lines = expenseDebits
            .Select(kv => new LedgerLine(kv.Key, Math.Round(kv.Value, 4, MidpointRounding.ToEven), 0m, "Operating expense"))
            .ToList();

        if (voucher.TaxTotal > 0m)
        {
            var vatInput = await _accounts.ResolveAsync("Expense", PostingKeys.VatInput, PostingSelector.None, ct);
            if (vatInput.IsFailure)
            {
                return vatInput.Error;
            }

            lines.Add(new LedgerLine(vatInput.Value, voucher.TaxTotal, 0m, "VAT input (PPN Masukan)"));
        }

        if (voucher.IsOnAccount)
        {
            var apControl = await _accounts.ResolveAsync("Expense", PostingKeys.ApControl, PostingSelector.None, ct);
            if (apControl.IsFailure)
            {
                return apControl.Error;
            }

            lines.Add(new LedgerLine(apControl.Value, 0m, voucher.GrandTotal, "Accounts payable", voucher.SupplierId));
        }
        else
        {
            lines.Add(new LedgerLine(voucher.CashAccountId!.Value, 0m, voucher.GrandTotal, "Cash / bank"));
        }

        return lines;
    }
}
