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

public sealed record PostingRuleDto(
    Guid Id, string EventType, string RuleKey, Guid AccountId, string AccountCode, string AccountName,
    Guid? ProductCategoryId, Guid? WarehouseId, Guid? TaxCodeId, Guid? BankAccountId, bool IsActive);

public sealed record PostingRuleHealthIssue(string RuleKey, string Problem);

public sealed record PostingRuleHealthDto(bool IsHealthy, IReadOnlyList<PostingRuleHealthIssue> Issues);

public sealed record SubledgerOpenItemDto(
    Guid Id, string Type, Guid PartyId, string SourceType, string DocumentNo,
    DateOnly DocumentDate, DateOnly DueDate, string Currency,
    decimal OriginalAmount, decimal SettledAmount, decimal OutstandingAmount, string Status);

public sealed record AgingRowDto(
    Guid PartyId, decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90,
    decimal Days90Plus, decimal Total);

public sealed record AgingReportDto(
    string Type, DateOnly AsOfDate, IReadOnlyList<AgingRowDto> Rows,
    decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Days90Plus, decimal Total);
