namespace Accountrack.Accounting.Application.Contracts;

public sealed record AccountDto(
    Guid Id, string Code, string Name, string Type, string NormalBalance,
    bool IsControlAccount, string ControlType, bool AllowPosting, bool IsActive, bool IsSystem);

public sealed record FiscalPeriodDto(Guid Id, int PeriodNo, DateOnly StartDate, DateOnly EndDate, string Status);

public sealed record FiscalYearDto(Guid Id, int Year, DateOnly StartDate, DateOnly EndDate, bool IsClosed,
    IReadOnlyList<FiscalPeriodDto> Periods);

public sealed record JournalLineDto(Guid AccountId, decimal Debit, decimal Credit, string? Description);

public sealed record JournalEntryDto(
    Guid Id, string EntryNo, DateOnly Date, string Currency, string Status, string Source,
    string Description, decimal TotalDebit, decimal TotalCredit, IReadOnlyList<JournalLineDto> Lines);
