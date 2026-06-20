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

// ---- Cash Flow Statement ----
public sealed record CashFlowLineDto(string AccountCode, string AccountName, decimal Amount);

public sealed record CashFlowSectionDto(IReadOnlyList<CashFlowLineDto> Lines, decimal Total);

public sealed record CashFlowStatementDto(
    DateOnly? FromDate, DateOnly? ToDate,
    decimal NetIncome,
    CashFlowSectionDto Operating,
    CashFlowSectionDto Investing,
    CashFlowSectionDto Financing,
    decimal NetChangeInCash,
    decimal OpeningCash, decimal ClosingCash, bool IsReconciled);

/// <summary>
/// Cash Flow Statement for a period, derived from the GL by the <b>indirect method</b> (ADR-0008).
/// Starts from net income and adjusts for the period movement of every non-cash balance-sheet
/// account: non-cash assets and operating liabilities are classified as Operating working capital,
/// equity movements (e.g. owner capital) as Financing. Cash &amp; bank accounts (the 10xx code band,
/// per the dashboard convention) are the reconciling target, never an activity line. By the
/// double-entry identity the three sections always sum to the actual change in cash, so the
/// statement reconciles opening + net change == closing cash. Investing (non-current assets) and
/// financing-debt are refined in a later phase when those accounts exist.
/// </summary>
public sealed record GetCashFlowStatementQuery(DateOnly? FromDate, DateOnly? ToDate) : IQuery<CashFlowStatementDto>;

public sealed class GetCashFlowStatementHandler : IQueryHandler<GetCashFlowStatementQuery, CashFlowStatementDto>
{
    private readonly IAccountingReadStore _store;

    public GetCashFlowStatementHandler(IAccountingReadStore store) => _store = store;

    private static bool IsCash(TrialBalanceRow r) =>
        r.AccountType == "Asset" && r.AccountCode.StartsWith("10", StringComparison.Ordinal);

    private static decimal CashBalance(IEnumerable<TrialBalanceRow> rows) =>
        rows.Where(IsCash).Sum(r => r.Debit - r.Credit);

    public async Task<Result<CashFlowStatementDto>> Handle(GetCashFlowStatementQuery request, CancellationToken ct)
    {
        var period = await _store.GetTrialBalanceAsync(request.FromDate, request.ToDate, ct);

        var netIncome =
            period.Where(r => r.AccountType == "Revenue").Sum(r => r.Credit - r.Debit)
            - period.Where(r => r.AccountType == "Expense").Sum(r => r.Debit - r.Credit);

        var operating = new List<CashFlowLineDto>();
        var investing = new List<CashFlowLineDto>();
        var financing = new List<CashFlowLineDto>();

        foreach (var r in period)
        {
            if (IsCash(r))
            {
                continue; // reconciling target, not an activity
            }

            switch (r.AccountType)
            {
                case "Asset":
                    // an increase in a non-cash asset consumes cash
                    var assetAdj = -(r.Debit - r.Credit);
                    if (assetAdj != 0) operating.Add(new CashFlowLineDto(r.AccountCode, r.AccountName, assetAdj));
                    break;
                case "Liability":
                    var liabAdj = r.Credit - r.Debit;
                    if (liabAdj != 0) operating.Add(new CashFlowLineDto(r.AccountCode, r.AccountName, liabAdj));
                    break;
                case "Equity":
                    var eqAdj = r.Credit - r.Debit;
                    if (eqAdj != 0) financing.Add(new CashFlowLineDto(r.AccountCode, r.AccountName, eqAdj));
                    break;
            }
        }

        var operatingTotal = netIncome + operating.Sum(l => l.Amount);
        var investingTotal = investing.Sum(l => l.Amount);
        var financingTotal = financing.Sum(l => l.Amount);
        var netChange = operatingTotal + investingTotal + financingTotal;

        // Opening cash = cash balance up to the day before the period; closing = cash to period end.
        var opening = request.FromDate is { } from
            ? CashBalance(await _store.GetTrialBalanceAsync(null, from.AddDays(-1), ct))
            : 0m;
        var actualClosing = CashBalance(await _store.GetTrialBalanceAsync(null, request.ToDate, ct));
        var closing = opening + netChange;

        return new CashFlowStatementDto(
            request.FromDate, request.ToDate, netIncome,
            new CashFlowSectionDto(operating, operatingTotal),
            new CashFlowSectionDto(investing, investingTotal),
            new CashFlowSectionDto(financing, financingTotal),
            netChange, opening, closing, Math.Abs(closing - actualClosing) < 0.005m);
    }
}
