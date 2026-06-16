using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Contracts;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Company;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Accounting.Application.Features;

/// <summary>
/// Home-dashboard finance KPIs, derived from the GL + AR/AP subledgers (never transactional tables):
/// cash &amp; bank balance, AR/AP outstanding and overdue, and this month's revenue/expense/net.
/// </summary>
public sealed record GetDashboardSummaryQuery : IQuery<DashboardSummaryDto>;

public sealed class GetDashboardSummaryHandler : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IAccountingReadStore _readStore;
    private readonly ISubledgerRepository _subledger;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public GetDashboardSummaryHandler(
        IAccountingReadStore readStore, ISubledgerRepository subledger,
        ICompanyDirectory companies, ITenantContext tenant, IClock clock)
    {
        _readStore = readStore;
        _subledger = subledger;
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
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        // Cash & bank: GL balance of asset accounts in the cash/bank code band (10xx) — by convention
        // 1000 Cash, 1010 Bank (AR 1100 / Inventory 1200 are control accounts, excluded).
        var allTime = await _readStore.GetTrialBalanceAsync(null, null, ct);
        var cashAndBank = allTime
            .Where(r => r.AccountType == nameof(AccountType.Asset) && r.AccountCode.StartsWith("10", StringComparison.Ordinal))
            .Sum(r => r.Balance);

        // This month's P&L from the GL.
        var period = await _readStore.GetTrialBalanceAsync(monthStart, monthEnd, ct);
        var revenue = period.Where(r => r.AccountType == nameof(AccountType.Revenue)).Sum(r => r.Credit - r.Debit);
        var expense = period.Where(r => r.AccountType == nameof(AccountType.Expense)).Sum(r => r.Debit - r.Credit);

        // AR / AP from the subledgers (open items).
        var arItems = await _subledger.ListAsync(SubledgerType.Receivable, partyId: null, includeSettled: false, ct);
        var apItems = await _subledger.ListAsync(SubledgerType.Payable, partyId: null, includeSettled: false, ct);

        var ar = arItems.Sum(i => i.OutstandingAmount.Amount);
        var ap = apItems.Sum(i => i.OutstandingAmount.Amount);
        var arOverdue = arItems.Where(i => i.DueDate < today).Sum(i => i.OutstandingAmount.Amount);
        var apOverdue = apItems.Where(i => i.DueDate < today).Sum(i => i.OutstandingAmount.Amount);

        return new DashboardSummaryDto(
            currency, today, cashAndBank, ar, ap, arOverdue, apOverdue, revenue, expense, revenue - expense);
    }
}
