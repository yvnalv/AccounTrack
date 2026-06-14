using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Features;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Accounting.UnitTests;

public class FinancialReportsTests
{
    private readonly IAccountingReadStore _store = Substitute.For<IAccountingReadStore>();

    // A simple set: Cash 1000 (Dr 1,000,000), Sales Revenue 4000 (Cr 1,000,000),
    // plus COGS 5000 (Dr 600,000) and Inventory 1200 (Cr 600,000) for a sale of goods.
    private static IReadOnlyList<TrialBalanceRow> SampleRows() => new List<TrialBalanceRow>
    {
        new("1000", "Cash", "Asset", 1_000_000m, 0m, 1_000_000m),
        new("1200", "Inventory", "Asset", 0m, 600_000m, -600_000m),
        new("4000", "Sales Revenue", "Revenue", 0m, 1_000_000m, -1_000_000m),
        new("5000", "COGS", "Expense", 600_000m, 0m, 600_000m),
    };

    [Fact]
    public async Task ProfitAndLoss_computes_revenue_expense_and_net_profit()
    {
        _store.GetTrialBalanceAsync(Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(SampleRows());

        var result = await new GetProfitAndLossHandler(_store)
            .Handle(new GetProfitAndLossQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var pl = result.Value;
        pl.TotalRevenue.Should().Be(1_000_000m);
        pl.TotalExpenses.Should().Be(600_000m);
        pl.NetProfit.Should().Be(400_000m);
        pl.Revenue.Should().ContainSingle(l => l.AccountCode == "4000" && l.Amount == 1_000_000m);
        pl.Expenses.Should().ContainSingle(l => l.AccountCode == "5000" && l.Amount == 600_000m);
    }

    [Fact]
    public async Task BalanceSheet_balances_with_current_earnings_in_equity()
    {
        _store.GetTrialBalanceAsync(Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(SampleRows());

        var result = await new GetBalanceSheetHandler(_store)
            .Handle(new GetBalanceSheetQuery(new DateOnly(2026, 6, 30)), CancellationToken.None);

        var bs = result.Value;
        // Assets = Cash 1,000,000 − Inventory 600,000 = 400,000
        bs.TotalAssets.Should().Be(400_000m);
        bs.TotalLiabilities.Should().Be(0m);
        // Current earnings = revenue 1,000,000 − expense 600,000 = 400,000 → equity 400,000
        bs.CurrentEarnings.Should().Be(400_000m);
        bs.TotalEquity.Should().Be(400_000m);
        bs.TotalLiabilitiesAndEquity.Should().Be(400_000m);
        bs.IsBalanced.Should().BeTrue();
    }
}
