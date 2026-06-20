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

    [Fact]
    public async Task CashFlow_indirect_method_reconciles_to_change_in_cash()
    {
        // Sale of goods: net income 400,000; inventory fell 600,000 (a source of cash).
        _store.GetTrialBalanceAsync(Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(SampleRows());

        var result = await new GetCashFlowStatementHandler(_store)
            .Handle(new GetCashFlowStatementQuery(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var cf = result.Value;
        cf.NetIncome.Should().Be(400_000m);
        // releasing 600,000 of inventory is a positive operating adjustment
        cf.Operating.Lines.Should().ContainSingle(l => l.AccountCode == "1200" && l.Amount == 600_000m);
        cf.Operating.Total.Should().Be(1_000_000m);
        cf.NetChangeInCash.Should().Be(1_000_000m);   // equals the Dr to Cash 1000
        cf.OpeningCash.Should().Be(0m);
        cf.ClosingCash.Should().Be(1_000_000m);
        cf.IsReconciled.Should().BeTrue();
    }

    [Fact]
    public async Task GeneralLedger_carries_opening_balance_into_a_running_balance()
    {
        var acc = Guid.NewGuid();
        var from = new DateOnly(2026, 6, 1);
        // Opening: Cash 1000 had a prior balance of 1,000,000 (Dr) before the period.
        _store.GetTrialBalanceAsync(null, from.AddDays(-1), Arg.Any<CancellationToken>())
            .Returns(new List<TrialBalanceRow> { new("1000", "Cash", "Asset", 1_000_000m, 0m, 1_000_000m) });
        _store.GetGeneralLedgerAsync(acc, from, Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new List<GeneralLedgerLineRow>
            {
                new(acc, "1000", "Cash", "Asset", new DateOnly(2026, 6, 5), "JE-1", "CustomerPayment", null, "Receipt", 500_000m, 0m),
                new(acc, "1000", "Cash", "Asset", new DateOnly(2026, 6, 9), "JE-2", "Expense", null, "Rent", 0m, 200_000m),
            });

        var result = await new GetGeneralLedgerHandler(_store)
            .Handle(new GetGeneralLedgerQuery(acc, from, new DateOnly(2026, 6, 30)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var account = result.Value.Accounts.Should().ContainSingle().Subject;
        account.OpeningBalance.Should().Be(1_000_000m);
        account.Entries.Should().HaveCount(2);
        account.Entries[0].RunningBalance.Should().Be(1_500_000m);
        account.Entries[1].RunningBalance.Should().Be(1_300_000m);
        account.TotalDebit.Should().Be(500_000m);
        account.TotalCredit.Should().Be(200_000m);
        account.ClosingBalance.Should().Be(1_300_000m);
    }

    [Fact]
    public async Task GeneralLedger_without_from_date_has_zero_opening_and_groups_by_account()
    {
        var cash = Guid.NewGuid();
        var rev = Guid.NewGuid();
        _store.GetGeneralLedgerAsync(Arg.Any<Guid?>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new List<GeneralLedgerLineRow>
            {
                new(rev, "4000", "Sales Revenue", "Revenue", new DateOnly(2026, 6, 5), "JE-1", "SalesInvoice", null, null, 0m, 900_000m),
                new(cash, "1000", "Cash", "Asset", new DateOnly(2026, 6, 5), "JE-1", "SalesInvoice", null, null, 900_000m, 0m),
            });

        var result = await new GetGeneralLedgerHandler(_store)
            .Handle(new GetGeneralLedgerQuery(null, null, null), CancellationToken.None);

        var accounts = result.Value.Accounts;
        accounts.Should().HaveCount(2);
        accounts[0].AccountCode.Should().Be("1000");       // ordered by code
        accounts[0].OpeningBalance.Should().Be(0m);
        accounts[0].ClosingBalance.Should().Be(900_000m);
        accounts[1].AccountCode.Should().Be("4000");
        accounts[1].ClosingBalance.Should().Be(-900_000m); // signed debit − credit
        await _store.DidNotReceive().GetTrialBalanceAsync(Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CashFlow_classifies_equity_movement_as_financing()
    {
        // Owner injects 500,000 capital: Dr Bank / Cr Retained Earnings.
        var rows = new List<TrialBalanceRow>
        {
            new("1010", "Bank", "Asset", 500_000m, 0m, 500_000m),
            new("3900", "Retained Earnings", "Equity", 0m, 500_000m, -500_000m),
        };
        _store.GetTrialBalanceAsync(Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(rows);

        var cf = (await new GetCashFlowStatementHandler(_store)
            .Handle(new GetCashFlowStatementQuery(null, null), CancellationToken.None)).Value;

        cf.NetIncome.Should().Be(0m);
        cf.Operating.Total.Should().Be(0m);
        cf.Financing.Lines.Should().ContainSingle(l => l.AccountCode == "3900" && l.Amount == 500_000m);
        cf.Financing.Total.Should().Be(500_000m);
        cf.NetChangeInCash.Should().Be(500_000m);
        cf.IsReconciled.Should().BeTrue();
    }
}
