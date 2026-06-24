using Accountrack.SharedKernel.Domain;

namespace Accountrack.Expenses.Domain;

/// <summary>Lifecycle of an expense voucher (ADR-0030, BR-EXP-5).</summary>
public enum ExpenseVoucherStatus
{
    /// <summary>Created in memory, not yet submitted/persisted.</summary>
    Draft = 0,
    /// <summary>Awaiting approval; no GL journal posted yet.</summary>
    PendingApproval = 1,
    /// <summary>Approved (or auto-approved) and posted to the GL.</summary>
    Posted = 2,
    /// <summary>Approval was rejected; never posted.</summary>
    Rejected = 3,
}

/// <summary>
/// An operating-expense voucher (ADR-0030). Posting it recognises the expense and pays it from a
/// cash/bank account, atomically: Dr Expense per category (+ Dr VAT Input where creditable) /
/// Cr Cash-Bank. Expense accounts are resolved per line via the posting-rule engine. When an approval
/// rule matches (BR-EXP-5) the voucher waits in <see cref="ExpenseVoucherStatus.PendingApproval"/> and
/// posts only once approved; otherwise it posts immediately. Immutable once posted; corrections are by
/// reversal (BR-EXP-4).
/// </summary>
public sealed class ExpenseVoucher : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<ExpenseVoucherLine> _lines = new();

    private ExpenseVoucher() { }

    private ExpenseVoucher(
        string number, DateOnly expenseDate, string? payeeName, Guid? cashAccountId, Guid? supplierId,
        DateOnly? dueDate, string currency, string? reference, string? notes)
    {
        Number = number;
        ExpenseDate = expenseDate;
        PayeeName = payeeName;
        CashAccountId = cashAccountId;
        SupplierId = supplierId;
        DueDate = dueDate;
        Currency = currency;
        Reference = reference;
        Notes = notes;
    }

    public string Number { get; private set; } = default!;
    public DateOnly ExpenseDate { get; private set; }

    /// <summary>Who was paid (free text — supplier or ad-hoc payee).</summary>
    public string? PayeeName { get; private set; }

    /// <summary>The cash/bank GL account the expense is paid from (null for an on-account voucher).</summary>
    public Guid? CashAccountId { get; private set; }

    /// <summary>The supplier owed when the expense is recorded on account (Cr AP) rather than paid.</summary>
    public Guid? SupplierId { get; private set; }

    /// <summary>Due date for an on-account voucher.</summary>
    public DateOnly? DueDate { get; private set; }

    public string Currency { get; private set; } = default!;
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    public decimal SubTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }

    /// <summary>The GL journal posted for this voucher (null until posted).</summary>
    public Guid? JournalEntryId { get; private set; }

    /// <summary>The AP open item created for an on-account voucher (null when paid from cash/bank).</summary>
    public Guid? ApOpenItemId { get; private set; }

    /// <summary>Where the voucher is in its approval/posting lifecycle.</summary>
    public ExpenseVoucherStatus Status { get; private set; } = ExpenseVoucherStatus.Draft;

    /// <summary>The approval request raised for this voucher (null when auto-approved).</summary>
    public Guid? ApprovalRequestId { get; private set; }

    public bool IsPendingApproval => Status == ExpenseVoucherStatus.PendingApproval;

    /// <summary>Whether this voucher is recorded on account (unpaid, Cr AP) rather than paid from cash/bank.</summary>
    public bool IsOnAccount => SupplierId is not null;

    public IReadOnlyList<ExpenseVoucherLine> Lines => _lines;

    /// <summary>A voucher paid immediately from a cash/bank account.</summary>
    public static ExpenseVoucher CreatePaid(
        string number, DateOnly expenseDate, string? payeeName, Guid cashAccountId, string currency,
        string? reference, string? notes) =>
        new(number, expenseDate, payeeName?.Trim(), cashAccountId, null, null,
            currency.Trim().ToUpperInvariant(), reference?.Trim(), notes?.Trim());

    /// <summary>A voucher recorded on account (unpaid): Cr Accounts Payable for the supplier.</summary>
    public static ExpenseVoucher CreateOnAccount(
        string number, DateOnly expenseDate, string? payeeName, Guid supplierId, DateOnly dueDate,
        string currency, string? reference, string? notes) =>
        new(number, expenseDate, payeeName?.Trim(), null, supplierId, dueDate,
            currency.Trim().ToUpperInvariant(), reference?.Trim(), notes?.Trim());

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

    /// <summary>Marks the voucher as awaiting approval (no GL posted yet).</summary>
    public void MarkPendingApproval(Guid approvalRequestId)
    {
        Status = ExpenseVoucherStatus.PendingApproval;
        ApprovalRequestId = approvalRequestId;
    }

    /// <summary>Records the posted GL journal and moves the voucher to Posted.</summary>
    public void MarkPosted(Guid journalEntryId)
    {
        JournalEntryId = journalEntryId;
        Status = ExpenseVoucherStatus.Posted;
    }

    public void MarkRejected() => Status = ExpenseVoucherStatus.Rejected;

    public void SetApOpenItem(Guid apOpenItemId) => ApOpenItemId = apOpenItemId;

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
