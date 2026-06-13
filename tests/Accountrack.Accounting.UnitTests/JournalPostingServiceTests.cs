using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Services;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Accounting.UnitTests;

public class JournalPostingServiceTests
{
    private static readonly DateOnly Date = new(2026, 6, 13);

    private readonly IFiscalPeriodRepository _periods = Substitute.For<IFiscalPeriodRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IJournalRepository _journals = Substitute.For<IJournalRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public JournalPostingServiceTests() =>
        _clock.UtcNow.Returns(new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc));

    private JournalPostingService Service() => new(_periods, _accounts, _journals, _clock);

    private static FiscalPeriod OpenPeriod()
    {
        var period = FiscalYear.Create(2026).PeriodFor(Date)!;
        return period;
    }

    private (JournalEntry draft, Account a, Account b) BalancedDraft()
    {
        var a = Account.Create("1000", "Cash", AccountType.Asset);
        var b = Account.Create("4000", "Revenue", AccountType.Revenue);
        _accounts.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, Account> { [a.Id] = a, [b.Id] = b });

        var draft = JournalEntry.CreateDraft(Date, "IDR", JournalSource.Manual, null, "Sale");
        draft.AddLine(a.Id, 100m, 0m);
        draft.AddLine(b.Id, 0m, 100m);
        return (draft, a, b);
    }

    [Fact]
    public async Task Posts_a_balanced_journal_into_an_open_period()
    {
        var (draft, _, _) = BalancedDraft();
        _periods.GetPeriodForDateAsync(Date, Arg.Any<CancellationToken>()).Returns(OpenPeriod());

        var result = await Service().PostAsync(draft, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        draft.Status.Should().Be(JournalStatus.Posted);
        draft.EntryNo.Should().StartWith("JE/202606/");
        _journals.Received(1).Add(draft);
    }

    [Fact]
    public async Task Rejects_posting_when_no_period_exists()
    {
        var (draft, _, _) = BalancedDraft();
        _periods.GetPeriodForDateAsync(Date, Arg.Any<CancellationToken>()).Returns((FiscalPeriod?)null);

        var result = await Service().PostAsync(draft, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AccountingErrors.NoOpenPeriod);
    }

    [Fact]
    public async Task Rejects_posting_into_a_closed_period()
    {
        var (draft, _, _) = BalancedDraft();
        var closed = OpenPeriod();
        closed.Close();
        _periods.GetPeriodForDateAsync(Date, Arg.Any<CancellationToken>()).Returns(closed);

        var result = await Service().PostAsync(draft, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AccountingErrors.PeriodClosed);
    }

    [Fact]
    public async Task Rejects_posting_to_a_non_postable_account()
    {
        var (draft, a, _) = BalancedDraft();
        a.Deactivate();
        _periods.GetPeriodForDateAsync(Date, Arg.Any<CancellationToken>()).Returns(OpenPeriod());

        var result = await Service().PostAsync(draft, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNTING.ACCOUNT_NOT_POSTABLE");
    }

    [Fact]
    public async Task Rejects_an_unbalanced_journal()
    {
        var a = Account.Create("1000", "Cash", AccountType.Asset);
        var b = Account.Create("4000", "Revenue", AccountType.Revenue);
        var draft = JournalEntry.CreateDraft(Date, "IDR", JournalSource.Manual, null, "Bad");
        draft.AddLine(a.Id, 100m, 0m);
        draft.AddLine(b.Id, 0m, 60m);

        var result = await Service().PostAsync(draft, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AccountingErrors.Unbalanced);
    }
}
