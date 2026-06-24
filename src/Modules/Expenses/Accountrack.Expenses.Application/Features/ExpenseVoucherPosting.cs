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

        // Dr Expense per account (net) + Dr VAT Input (total creditable tax) / Cr Cash-Bank|AP (gross).
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

        var posting = new LedgerPostingRequest(
            voucher.ExpenseDate, LedgerSource.Expense, voucher.Id, $"Expense voucher {voucher.Number}", lines);

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
}
