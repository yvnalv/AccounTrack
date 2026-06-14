using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Accounting.Application.Features;

public sealed record ReportLineDto(string AccountCode, string AccountName, decimal Amount);

// ---- Profit & Loss ----
public sealed record ProfitAndLossDto(
    DateOnly? FromDate, DateOnly? ToDate,
    IReadOnlyList<ReportLineDto> Revenue, decimal TotalRevenue,
    IReadOnlyList<ReportLineDto> Expenses, decimal TotalExpenses,
    decimal NetProfit);

/// <summary>Profit &amp; Loss for a period, derived from the GL (ADR-0008).</summary>
public sealed record GetProfitAndLossQuery(DateOnly? FromDate, DateOnly? ToDate) : IQuery<ProfitAndLossDto>;

public sealed class GetProfitAndLossHandler : IQueryHandler<GetProfitAndLossQuery, ProfitAndLossDto>
{
    private readonly IAccountingReadStore _store;

    public GetProfitAndLossHandler(IAccountingReadStore store) => _store = store;

    public async Task<Result<ProfitAndLossDto>> Handle(GetProfitAndLossQuery request, CancellationToken ct)
    {
        var rows = await _store.GetTrialBalanceAsync(request.FromDate, request.ToDate, ct);

        var revenue = rows.Where(r => r.AccountType == "Revenue")
            .Select(r => new ReportLineDto(r.AccountCode, r.AccountName, r.Credit - r.Debit))
            .Where(l => l.Amount != 0).ToList();

        var expenses = rows.Where(r => r.AccountType == "Expense")
            .Select(r => new ReportLineDto(r.AccountCode, r.AccountName, r.Debit - r.Credit))
            .Where(l => l.Amount != 0).ToList();

        var totalRevenue = revenue.Sum(l => l.Amount);
        var totalExpenses = expenses.Sum(l => l.Amount);

        return new ProfitAndLossDto(
            request.FromDate, request.ToDate, revenue, totalRevenue, expenses, totalExpenses,
            totalRevenue - totalExpenses);
    }
}

// ---- Balance Sheet ----
public sealed record BalanceSheetDto(
    DateOnly AsOfDate,
    IReadOnlyList<ReportLineDto> Assets, decimal TotalAssets,
    IReadOnlyList<ReportLineDto> Liabilities, decimal TotalLiabilities,
    IReadOnlyList<ReportLineDto> Equity, decimal CurrentEarnings, decimal TotalEquity,
    decimal TotalLiabilitiesAndEquity, bool IsBalanced);

/// <summary>
/// Balance Sheet as of a date, derived from the GL. Net income to date (revenue − expense) is
/// shown as current earnings within equity (year-end close to retained earnings is a later phase).
/// </summary>
public sealed record GetBalanceSheetQuery(DateOnly AsOfDate) : IQuery<BalanceSheetDto>;

public sealed class GetBalanceSheetHandler : IQueryHandler<GetBalanceSheetQuery, BalanceSheetDto>
{
    private readonly IAccountingReadStore _store;

    public GetBalanceSheetHandler(IAccountingReadStore store) => _store = store;

    public async Task<Result<BalanceSheetDto>> Handle(GetBalanceSheetQuery request, CancellationToken ct)
    {
        var rows = await _store.GetTrialBalanceAsync(null, request.AsOfDate, ct);

        var assets = rows.Where(r => r.AccountType == "Asset")
            .Select(r => new ReportLineDto(r.AccountCode, r.AccountName, r.Debit - r.Credit))
            .Where(l => l.Amount != 0).ToList();

        var liabilities = rows.Where(r => r.AccountType == "Liability")
            .Select(r => new ReportLineDto(r.AccountCode, r.AccountName, r.Credit - r.Debit))
            .Where(l => l.Amount != 0).ToList();

        var equity = rows.Where(r => r.AccountType == "Equity")
            .Select(r => new ReportLineDto(r.AccountCode, r.AccountName, r.Credit - r.Debit))
            .Where(l => l.Amount != 0).ToList();

        var currentEarnings =
            rows.Where(r => r.AccountType == "Revenue").Sum(r => r.Credit - r.Debit)
            - rows.Where(r => r.AccountType == "Expense").Sum(r => r.Debit - r.Credit);

        var totalAssets = assets.Sum(l => l.Amount);
        var totalLiabilities = liabilities.Sum(l => l.Amount);
        var totalEquity = equity.Sum(l => l.Amount) + currentEarnings;
        var totalLiabilitiesAndEquity = totalLiabilities + totalEquity;

        return new BalanceSheetDto(
            request.AsOfDate, assets, totalAssets, liabilities, totalLiabilities, equity, currentEarnings,
            totalEquity, totalLiabilitiesAndEquity, totalAssets == totalLiabilitiesAndEquity);
    }
}
