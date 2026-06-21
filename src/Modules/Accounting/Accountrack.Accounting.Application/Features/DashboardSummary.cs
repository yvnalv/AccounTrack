using System.Globalization;
using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Contracts;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Accounting.Application.Features;

/// <summary>
/// Home-dashboard finance insights, derived from the GL + AR/AP subledgers (never transactional
/// tables): cash &amp; bank, AR/AP outstanding + overdue + aging, this/prev month P&amp;L, inventory
/// value, a 6-month revenue/expense/profit trend, expense composition, and the top debtors/creditors.
/// </summary>
public sealed record GetDashboardSummaryQuery : IQuery<DashboardSummaryDto>;

public sealed class GetDashboardSummaryHandler : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private const int TrendMonths = 6;
    private const int TopN = 5;

    private readonly IAccountingReadStore _readStore;
    private readonly ISubledgerRepository _subledger;
    private readonly IMasterDataLookup _lookup;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public GetDashboardSummaryHandler(
        IAccountingReadStore readStore, ISubledgerRepository subledger, IMasterDataLookup lookup,
        ICompanyDirectory companies, ITenantContext tenant, IClock clock)
    {
        _readStore = readStore;
        _subledger = subledger;
        _lookup = lookup;
        _companies = companies;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<Result<DashboardSummaryDto>> Handle(GetDashboardSummaryQuery request, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        var currency = company?.FunctionalCurrency ?? "IDR";

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // Balance-sheet snapshots from the all-time trial balance.
        var allTime = await _readStore.GetTrialBalanceAsync(null, null, ct);
        var cashAndBank = allTime
            .Where(r => r.AccountType == nameof(AccountType.Asset) && r.AccountCode.StartsWith("10", StringComparison.Ordinal))
            .Sum(r => r.Balance);
        var inventoryValue = allTime
            .Where(r => r.AccountType == nameof(AccountType.Asset) && r.AccountCode.StartsWith("12", StringComparison.Ordinal))
            .Sum(r => r.Balance);

        // 6-month revenue / expense / profit trend (oldest → newest); this month is the last point.
        var trend = new List<DashboardMonthlyPointDto>(TrendMonths);
        List<TrialBalanceRow> currentMonthRows = new();
        for (var i = TrendMonths - 1; i >= 0; i--)
        {
            var mStart = monthStart.AddMonths(-i);
            var mEnd = mStart.AddMonths(1).AddDays(-1);
            var rows = await _readStore.GetTrialBalanceAsync(mStart, mEnd, ct);
            if (i == 0) currentMonthRows = rows.ToList();

            var rev = rows.Where(r => r.AccountType == nameof(AccountType.Revenue)).Sum(r => r.Credit - r.Debit);
            var exp = rows.Where(r => r.AccountType == nameof(AccountType.Expense)).Sum(r => r.Debit - r.Credit);
            trend.Add(new DashboardMonthlyPointDto(
                mStart.ToString("yyyy-MM", CultureInfo.InvariantCulture), rev, exp, rev - exp));
        }

        var thisMonth = trend[^1];
        var prevMonth = trend.Count >= 2 ? trend[^2] : new DashboardMonthlyPointDto("", 0m, 0m, 0m);

        // Expense composition this month (each expense account that moved), largest first.
        var expenseByCategory = currentMonthRows
            .Where(r => r.AccountType == nameof(AccountType.Expense))
            .Select(r => new DashboardNamedAmountDto(r.AccountName, r.Debit - r.Credit))
            .Where(x => x.Amount != 0m)
            .OrderByDescending(x => x.Amount)
            .ToList();

        // AR / AP open items → totals, overdue, aging, and top debtors/creditors.
        var arItems = await _subledger.ListAsync(SubledgerType.Receivable, null, false, ct);
        var apItems = await _subledger.ListAsync(SubledgerType.Payable, null, false, ct);

        var ar = arItems.Sum(i => i.OutstandingAmount.Amount);
        var ap = apItems.Sum(i => i.OutstandingAmount.Amount);
        var arOverdue = arItems.Where(i => i.DueDate < today).Sum(i => i.OutstandingAmount.Amount);
        var apOverdue = apItems.Where(i => i.DueDate < today).Sum(i => i.OutstandingAmount.Amount);

        var topParties = await ResolveTopPartiesAsync(arItems, apItems, ct);

        return new DashboardSummaryDto(
            currency, today, cashAndBank, ar, ap, arOverdue, apOverdue,
            thisMonth.Revenue, thisMonth.Expense, thisMonth.Profit,
            prevMonth.Revenue, prevMonth.Expense,
            inventoryValue,
            arItems.Count(i => i.DueDate < today),
            apItems.Count(i => i.DueDate < today),
            trend,
            Aging(arItems, today), Aging(apItems, today),
            expenseByCategory,
            topParties.Receivables, topParties.Payables);
    }

    private static DashboardAgingDto Aging(IEnumerable<SubledgerOpenItem> items, DateOnly today)
    {
        decimal current = 0, d30 = 0, d60 = 0, d90 = 0, d90p = 0;
        foreach (var i in items)
        {
            var amount = i.OutstandingAmount.Amount;
            var overdue = today.DayNumber - i.DueDate.DayNumber;
            if (overdue <= 0) current += amount;
            else if (overdue <= 30) d30 += amount;
            else if (overdue <= 60) d60 += amount;
            else if (overdue <= 90) d90 += amount;
            else d90p += amount;
        }

        return new DashboardAgingDto(current, d30, d60, d90, d90p);
    }

    private async Task<(IReadOnlyList<DashboardNamedAmountDto> Receivables, IReadOnlyList<DashboardNamedAmountDto> Payables)>
        ResolveTopPartiesAsync(
            IReadOnlyList<SubledgerOpenItem> arItems, IReadOnlyList<SubledgerOpenItem> apItems, CancellationToken ct)
    {
        static List<(Guid Party, decimal Amount)> Top(IReadOnlyList<SubledgerOpenItem> items) =>
            items.GroupBy(i => i.PartyId)
                .Select(g => (Party: g.Key, Amount: g.Sum(i => i.OutstandingAmount.Amount)))
                .Where(x => x.Amount > 0)
                .OrderByDescending(x => x.Amount)
                .Take(TopN)
                .ToList();

        var topAr = Top(arItems);
        var topAp = Top(apItems);

        var ids = topAr.Select(x => x.Party).Concat(topAp.Select(x => x.Party)).Distinct().ToArray();
        var names = ids.Length == 0
            ? new Dictionary<Guid, string>()
            : await _lookup.ResolveNamesAsync(ids, ct);

        DashboardNamedAmountDto Map((Guid Party, decimal Amount) x) =>
            new(names.GetValueOrDefault(x.Party, x.Party.ToString()), x.Amount);

        return (topAr.Select(Map).ToList(), topAp.Select(Map).ToList());
    }
}
