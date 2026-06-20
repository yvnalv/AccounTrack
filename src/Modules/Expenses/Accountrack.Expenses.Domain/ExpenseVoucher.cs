using Accountrack.SharedKernel.Domain;

namespace Accountrack.Expenses.Domain;

/// <summary>
/// An operating-expense voucher (ADR-0030). Posting it recognises the expense and pays it from a
/// cash/bank account, atomically: Dr Expense per category (+ Dr VAT Input where creditable) /
/// Cr Cash-Bank. Expense accounts are resolved per line via the posting-rule engine. Immutable once
/// posted; corrections are by reversal (BR-EXP-4).
/// </summary>
public sealed class ExpenseVoucher : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<ExpenseVoucherLine> _lines = new();

    private ExpenseVoucher() { }

    private ExpenseVoucher(
        string number, DateOnly expenseDate, string? payeeName, Guid cashAccountId, string currency,
        string? reference, string? notes)
    {
        Number = number;
        ExpenseDate = expenseDate;
        PayeeName = payeeName;
        CashAccountId = cashAccountId;
        Currency = currency;
        Reference = reference;
        Notes = notes;
    }

    public string Number { get; private set; } = default!;
    public DateOnly ExpenseDate { get; private set; }

    /// <summary>Who was paid (free text — supplier or ad-hoc payee).</summary>
    public string? PayeeName { get; private set; }

    /// <summary>The cash/bank GL account the expense is paid from.</summary>
    public Guid CashAccountId { get; private set; }

    public string Currency { get; private set; } = default!;
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    public decimal SubTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }

    /// <summary>The GL journal posted for this voucher.</summary>
    public Guid? JournalEntryId { get; private set; }

    public IReadOnlyList<ExpenseVoucherLine> Lines => _lines;

    public static ExpenseVoucher Create(
        string number, DateOnly expenseDate, string? payeeName, Guid cashAccountId, string currency,
        string? reference, string? notes) =>
        new(number, expenseDate, payeeName?.Trim(), cashAccountId, currency.Trim().ToUpperInvariant(),
            reference?.Trim(), notes?.Trim());

    public void AddLine(Guid expenseCategoryId, string expenseRuleKey, string? description, decimal amount, decimal taxRate)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Expense line amount must be positive.");
        }

        if (taxRate is < 0 or > 1)
        {
            throw new InvalidOperationException("Tax rate must be a fraction between 0 and 1.");
        }

        _lines.Add(ExpenseVoucherLine.Create(expenseCategoryId, expenseRuleKey, description, amount, taxRate));
        Recalculate();
    }

    public void SetJournal(Guid journalEntryId) => JournalEntryId = journalEntryId;

    private void Recalculate()
    {
        SubTotal = Math.Round(_lines.Sum(l => l.Amount), 4, MidpointRounding.ToEven);
        TaxTotal = Math.Round(_lines.Sum(l => l.LineTax), 4, MidpointRounding.ToEven);
        GrandTotal = SubTotal + TaxTotal;
    }
}

/// <summary>An expense-voucher line: a net amount against a category, plus optional creditable VAT.</summary>
public sealed class ExpenseVoucherLine : Entity
{
    private ExpenseVoucherLine() { }

    private ExpenseVoucherLine(Guid expenseCategoryId, string expenseRuleKey, string? description, decimal amount, decimal taxRate)
    {
        ExpenseCategoryId = expenseCategoryId;
        ExpenseRuleKey = expenseRuleKey;
        Description = description;
        Amount = amount;
        TaxRate = taxRate;
        LineTax = Math.Round(amount * taxRate, 4, MidpointRounding.ToEven);
        LineTotal = amount + LineTax;
    }

    public Guid ExpenseVoucherId { get; private set; }
    public Guid ExpenseCategoryId { get; private set; }

    /// <summary>Snapshot of the category's posting-rule key, used to resolve the expense account.</summary>
    public string ExpenseRuleKey { get; private set; } = default!;

    public string? Description { get; private set; }

    /// <summary>The net (pre-tax) expense amount.</summary>
    public decimal Amount { get; private set; }

    public decimal TaxRate { get; private set; }
    public decimal LineTax { get; private set; }
    public decimal LineTotal { get; private set; }

    internal static ExpenseVoucherLine Create(Guid expenseCategoryId, string expenseRuleKey, string? description, decimal amount, decimal taxRate) =>
        new(expenseCategoryId, expenseRuleKey, description?.Trim(), amount, taxRate);
}
