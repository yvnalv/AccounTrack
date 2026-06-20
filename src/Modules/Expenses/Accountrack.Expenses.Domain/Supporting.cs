using Accountrack.SharedKernel.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Expenses.Domain;

/// <summary>Per-company gapless counter for expense-voucher numbers.</summary>
public sealed class ExpenseVoucherNumberSequence : TenantOwnedEntity
{
    private ExpenseVoucherNumberSequence() { }

    public ExpenseVoucherNumberSequence(int next = 1) => Next = next;

    public int Next { get; private set; }

    public string Take(DateOnly date)
    {
        var value = Next;
        Next++;
        return $"EXP/{date.Year:D4}{date.Month:D2}/{value:D5}";
    }
}

public static class ExpenseErrors
{
    public static readonly Error CategoryNotFound =
        Error.NotFound("EXPENSES.CATEGORY_NOT_FOUND", "Expense category not found.");

    public static readonly Error CategoryCodeExists =
        Error.Conflict("EXPENSES.CATEGORY_CODE_EXISTS", "An expense category with this code already exists.");

    public static readonly Error VoucherNotFound =
        Error.NotFound("EXPENSES.VOUCHER_NOT_FOUND", "Expense voucher not found.");

    public static readonly Error NoLines =
        Error.BusinessRule("BR-EXP-1", "An expense voucher requires at least one line.", "EXPENSES.NO_LINES");

    public static Error LineCategoryNotFound(Guid categoryId) =>
        Error.Validation("EXPENSES.LINE_CATEGORY_NOT_FOUND", $"Expense category {categoryId} does not exist or is inactive.");
}
