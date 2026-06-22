using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Features;
using Accountrack.Accounting.Domain;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Accounting.UnitTests;

public class PeriodBalanceSnapshotTests
{
    private static readonly Account Cash = Account.CreateWithId(Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
    private static readonly Account Revenue = Account.CreateWithId(Guid.NewGuid(), "4000", "Sales Revenue", AccountType.Revenue);

    private readonly IFiscalPeriodRepository _periods = Substitute.For<IFiscalPeriodRepository>();
    private readonly IAccountingReadStore _store = Substitute.For<IAccountingReadStore>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IAccountingUnitOfWork _uow = Substitute.For<IAccountingUnitOfWork>();

    public PeriodBalanceSnapshotTests()
    {
        _accounts.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<Account> { Cash, Revenue });
    }

    private static FiscalPeriod FirstPeriod()
    {
        var year = FiscalYear.Create(2026);
        return year.Periods.OrderBy(p => p.PeriodNo).First();
    }

    [Fact]
    public async Task Closing_a_period_snapshots_the_cumulative_balances_and_clears_any_prior_snapshot()
    {
        var period = FirstPeriod();
        _periods.GetPeriodByIdAsync(period.Id, Arg.Any<CancellationToken>()).Returns(period);
        _store.GetTrialBalanceAsync(null, period.EndDate, Arg.Any<CancellationToken>())
            .Returns(new List<TrialBalanceRow>
            {
                new("1000", "Cash", "Asset", 1_000_000m, 0m, 1_000_000m),
                new("4000", "Sales Revenue", "Revenue", 0m, 1_000_000m, -1_000_000m),
                new("9999", "Unmapped", "Asset", 5m, 0m, 5m),   // not in chart → skipped
                new("5000", "Empty", "Expense", 0m, 0m, 0m),    // zero → skipped
            });

        var captured = new List<PeriodBalance>();
        _periods.When(p => p.AddPeriodBalance(Arg.Any<PeriodBalance>())).Do(ci => captured.Add(ci.Arg<PeriodBalance>()));

        var result = await new CloseFiscalPeriodCommandHandler(_periods, _store, _accounts, _uow)
            .Handle(new CloseFiscalPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        period.Status.Should().Be(FiscalPeriodStatus.Closed);
        await _periods.Received(1).ClearPeriodBalancesAsync(period.Id, Arg.Any<CancellationToken>());
        captured.Should().HaveCount(2); // only the two mapped, non-zero rows
        captured.Should().Contain(b => b.AccountCode == "1000" && b.Debit == 1_000_000m);
        captured.Should().Contain(b => b.AccountCode == "4000" && b.Credit == 1_000_000m);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reopening_a_period_clears_its_stale_snapshot()
    {
        var period = FirstPeriod();
        period.Close();
        _periods.GetPeriodByIdAsync(period.Id, Arg.Any<CancellationToken>()).Returns(period);

        var result = await new ReopenFiscalPeriodCommandHandler(_periods, _uow)
            .Handle(new ReopenFiscalPeriodCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        period.Status.Should().Be(FiscalPeriodStatus.Open);
        await _periods.Received(1).ClearPeriodBalancesAsync(period.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rebuild_recomputes_the_snapshot_from_the_ledger()
    {
        var period = FirstPeriod();
        period.Close();
        _periods.GetPeriodByIdAsync(period.Id, Arg.Any<CancellationToken>()).Returns(period);
        _store.GetTrialBalanceAsync(null, period.EndDate, Arg.Any<CancellationToken>())
            .Returns(new List<TrialBalanceRow> { new("1000", "Cash", "Asset", 250_000m, 0m, 250_000m) });

        var captured = new List<PeriodBalance>();
        _periods.When(p => p.AddPeriodBalance(Arg.Any<PeriodBalance>())).Do(ci => captured.Add(ci.Arg<PeriodBalance>()));

        var result = await new RebuildPeriodBalancesCommandHandler(_periods, _store, _accounts, _uow)
            .Handle(new RebuildPeriodBalancesCommand(period.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _periods.Received(1).ClearPeriodBalancesAsync(period.Id, Arg.Any<CancellationToken>());
        captured.Should().ContainSingle(b => b.AccountCode == "1000" && b.Debit == 250_000m);
    }

    [Fact]
    public async Task Closing_an_unknown_period_is_rejected()
    {
        _periods.GetPeriodByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FiscalPeriod?)null);

        var result = await new CloseFiscalPeriodCommandHandler(_periods, _store, _accounts, _uow)
            .Handle(new CloseFiscalPeriodCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNTING.PERIOD_NOT_FOUND");
    }
}
