namespace Accountrack.Expenses.Application.Contracts;

public sealed record ExpenseCategoryDto(Guid Id, string Code, string Name, string PostingRuleKey, bool IsActive);

public sealed record ExpenseVoucherLineDto(
    Guid ExpenseCategoryId, string? Description, decimal Amount, decimal TaxRate, decimal LineTax, decimal LineTotal);

public sealed record ExpenseVoucherDto(
    Guid Id, string Number, DateOnly ExpenseDate, string? PayeeName, Guid CashAccountId, string Currency,
    decimal SubTotal, decimal TaxTotal, decimal GrandTotal, Guid? JournalEntryId, string? Reference, string? Notes,
    IReadOnlyList<ExpenseVoucherLineDto> Lines);

public sealed record ExpenseVoucherSummaryDto(
    Guid Id, string Number, DateOnly ExpenseDate, string? PayeeName, decimal GrandTotal, Guid? JournalEntryId);
