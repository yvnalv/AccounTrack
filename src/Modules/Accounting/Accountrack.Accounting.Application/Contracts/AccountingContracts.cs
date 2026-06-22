namespace Accountrack.Accounting.Application.Contracts;

public sealed record AccountDto(
    Guid Id, string Code, string Name, string Type, string NormalBalance,
    bool IsControlAccount, string ControlType, bool AllowPosting, bool IsActive, bool IsSystem);

public sealed record FiscalPeriodDto(Guid Id, int PeriodNo, DateOnly StartDate, DateOnly EndDate, string Status);

public sealed record FiscalYearDto(Guid Id, int Year, DateOnly StartDate, DateOnly EndDate, bool IsClosed,
    IReadOnlyList<FiscalPeriodDto> Periods);

/// <summary>One account's cumulative closing balance captured when a period was closed (ADR-0022).</summary>
public sealed record PeriodBalanceDto(string AccountCode, string AccountName, decimal Debit, decimal Credit);

/// <summary>Result of a year-end close: the posted closing journal (null if there was nothing to
/// close) and the net income carried to retained earnings.</summary>
public sealed record CloseFiscalYearResult(Guid? JournalEntryId, decimal NetIncome);

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

/// <summary>Finance KPIs for the home dashboard (derived from the GL + AR/AP subledgers).</summary>
public sealed record DashboardMonthlyPointDto(string Month, decimal Revenue, decimal Expense, decimal Profit);

public sealed record DashboardAgingDto(
    decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal Days90Plus);

public sealed record DashboardNamedAmountDto(string Name, decimal Amount);

public sealed record DashboardSummaryDto(
    string Currency,
    DateOnly AsOfDate,
    decimal CashAndBank,
    decimal AccountsReceivable,
    decimal AccountsPayable,
    decimal OverdueReceivable,
    decimal OverduePayable,
    decimal RevenueThisMonth,
    decimal ExpenseThisMonth,
    decimal NetProfitThisMonth,
    decimal RevenuePrevMonth,
    decimal ExpensePrevMonth,
    decimal InventoryValue,
    int OverdueReceivableCount,
    int OverduePayableCount,
    IReadOnlyList<DashboardMonthlyPointDto> MonthlyTrend,
    DashboardAgingDto ArAging,
    DashboardAgingDto ApAging,
    IReadOnlyList<DashboardNamedAmountDto> ExpenseByCategory,
    IReadOnlyList<DashboardNamedAmountDto> TopReceivables,
    IReadOnlyList<DashboardNamedAmountDto> TopPayables);
