using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Features;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.SharedKernel.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Accounting.UnitTests;

public class DashboardSummaryTests
{
    private const string Idr = "IDR";
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 6, 16);

    private readonly IAccountingReadStore _readStore = Substitute.For<IAccountingReadStore>();
    private readonly ISubledgerRepository _subledger = Substitute.For<ISubledgerRepository>();
    private readonly IMasterDataLookup _lookup = Substitute.For<IMasterDataLookup>();
    private readonly ICompanyDirectory _companies = Substitute.For<ICompanyDirectory>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public DashboardSummaryTests()
    {
        _tenant.CompanyId.Returns(CompanyId);
        _companies.GetAsync(CompanyId, Arg.Any<CancellationToken>()).Returns(new CompanyInfo(CompanyId, "DEV", Idr, 1));
        _clock.UtcNow.Returns(Today.ToDateTime(TimeOnly.MinValue));
        _lookup.ResolveNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ci => (IReadOnlyDictionary<Guid, string>)new Dictionary<Guid, string>());

        // All-time trial balance (cash 1000 + bank 1010 = 1,500,000; AR/Inventory excluded).
        _readStore.GetTrialBalanceAsync(null, null, Arg.Any<CancellationToken>()).Returns(new List<TrialBalanceRow>
        {
            new("1000", "Cash", "Asset", 1_000_000m, 0m, 1_000_000m),
            new("1010", "Bank", "Asset", 600_000m, 100_000m, 500_000m),
            new("1100", "Accounts Receivable", "Asset", 9_000_000m, 0m, 9_000_000m), // control — excluded
            new("1200", "Inventory", "Asset", 2_000_000m, 0m, 2_000_000m),           // 12xx — excluded
        });

        // Current-month P&L (revenue 1,000,000; expense 600,000 -> net 400,000).
        _readStore.GetTrialBalanceAsync(Arg.Is<DateOnly?>(d => d != null), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new List<TrialBalanceRow>
            {
                new("4000", "Sales Revenue", "Revenue", 0m, 1_000_000m, -1_000_000m),
                new("5000", "COGS", "Expense", 600_000m, 0m, 600_000m),
            });
    }

    private static SubledgerOpenItem Item(SubledgerType type, decimal amount, DateOnly due)
    {
        var item = SubledgerOpenItem.Open(type, Guid.NewGuid(), JournalSource.Manual, null, "DOC",
            new DateOnly(2026, 5, 1), due, Money.Create(amount, Idr));
        return item;
    }

    [Fact]
    public async Task Summary_aggregates_cash_ar_ap_overdue_and_month_pnl()
    {
        _subledger.ListAsync(SubledgerType.Receivable, null, false, Arg.Any<CancellationToken>()).Returns(new List<SubledgerOpenItem>
        {
            Item(SubledgerType.Receivable, 700_000m, new DateOnly(2026, 7, 10)), // not due
            Item(SubledgerType.Receivable, 300_000m, new DateOnly(2026, 6, 1)),  // overdue
        });
        _subledger.ListAsync(SubledgerType.Payable, null, false, Arg.Any<CancellationToken>()).Returns(new List<SubledgerOpenItem>
        {
            Item(SubledgerType.Payable, 250_000m, new DateOnly(2026, 6, 10)), // overdue
        });

        var result = await new GetDashboardSummaryHandler(_readStore, _subledger, _lookup, _companies, _tenant, _clock)
            .Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        var s = result.Value;
        s.Currency.Should().Be(Idr);
        s.CashAndBank.Should().Be(1_500_000m);
        s.AccountsReceivable.Should().Be(1_000_000m);
        s.AccountsPayable.Should().Be(250_000m);
        s.OverdueReceivable.Should().Be(300_000m);
        s.OverduePayable.Should().Be(250_000m);
        s.RevenueThisMonth.Should().Be(1_000_000m);
        s.ExpenseThisMonth.Should().Be(600_000m);
        s.NetProfitThisMonth.Should().Be(400_000m);

        // Richer insights.
        s.InventoryValue.Should().Be(2_000_000m);
        s.MonthlyTrend.Should().HaveCount(6);
        s.MonthlyTrend[^1].Profit.Should().Be(400_000m);
        s.RevenuePrevMonth.Should().Be(1_000_000m);
        s.ArAging.Current.Should().Be(700_000m);     // due 2026-07-10, today 2026-06-16
        s.ArAging.Days1To30.Should().Be(300_000m);   // due 2026-06-01 → 15 days overdue
        s.ApAging.Days1To30.Should().Be(250_000m);   // due 2026-06-10 → 6 days overdue
        s.OverdueReceivableCount.Should().Be(1);
        s.TopReceivables.Should().HaveCount(2);
        s.ExpenseByCategory.Should().ContainSingle(c => c.Name == "COGS" && c.Amount == 600_000m);
    }
}
