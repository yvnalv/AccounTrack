using Accountrack.Accounting.Domain;
using FluentAssertions;
using Xunit;

namespace Accountrack.Accounting.UnitTests;

public class JournalEntryTests
{
    private static readonly DateOnly Date = new(2026, 6, 13);
    private static readonly DateTime Now = new(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc);

    private static JournalEntry Draft() =>
        JournalEntry.CreateDraft(Date, "IDR", JournalSource.Manual, null, "Test");

    [Fact]
    public void Balanced_journal_posts()
    {
        var je = Draft();
        je.AddLine(Guid.NewGuid(), debit: 100m, credit: 0m);
        je.AddLine(Guid.NewGuid(), debit: 0m, credit: 100m);

        je.IsBalanced.Should().BeTrue();
        je.Post("JE/202606/000001", Guid.NewGuid(), Now);
        je.Status.Should().Be(JournalStatus.Posted);
    }

    [Fact]
    public void Unbalanced_journal_cannot_post()
    {
        var je = Draft();
        je.AddLine(Guid.NewGuid(), 100m, 0m);
        je.AddLine(Guid.NewGuid(), 0m, 60m);

        je.IsBalanced.Should().BeFalse();
        FluentActions.Invoking(() => je.Post("X", Guid.NewGuid(), Now))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_line_must_be_exactly_one_of_debit_or_credit()
    {
        var je = Draft();
        FluentActions.Invoking(() => je.AddLine(Guid.NewGuid(), 100m, 100m))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => je.AddLine(Guid.NewGuid(), 0m, 0m))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Single_line_cannot_post()
    {
        var je = Draft();
        je.AddLine(Guid.NewGuid(), 100m, 0m);
        FluentActions.Invoking(() => je.Post("X", Guid.NewGuid(), Now))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reversal_mirrors_debits_and_credits_and_balances()
    {
        var arAccount = Guid.NewGuid();
        var revAccount = Guid.NewGuid();
        var je = Draft();
        je.AddLine(arAccount, 110m, 0m);
        je.AddLine(revAccount, 0m, 110m);
        je.Post("JE/202606/000001", Guid.NewGuid(), Now);

        var reversal = je.CreateReversal(Date, "Reversal");

        reversal.IsBalanced.Should().BeTrue();
        reversal.Lines.Should().HaveCount(2);
        reversal.Lines.Single(l => l.AccountId == arAccount).Credit.Amount.Should().Be(110m);
        reversal.Lines.Single(l => l.AccountId == revAccount).Debit.Amount.Should().Be(110m);

        je.MarkReversedBy(reversal.Id);
        je.Status.Should().Be(JournalStatus.Reversed);
    }

    [Fact]
    public void Posted_journal_is_immutable_to_new_lines()
    {
        var je = Draft();
        je.AddLine(Guid.NewGuid(), 100m, 0m);
        je.AddLine(Guid.NewGuid(), 0m, 100m);
        je.Post("JE/202606/000001", Guid.NewGuid(), Now);

        FluentActions.Invoking(() => je.AddLine(Guid.NewGuid(), 50m, 0m))
            .Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(AccountType.Asset, NormalBalance.Debit)]
    [InlineData(AccountType.Expense, NormalBalance.Debit)]
    [InlineData(AccountType.Liability, NormalBalance.Credit)]
    [InlineData(AccountType.Equity, NormalBalance.Credit)]
    [InlineData(AccountType.Revenue, NormalBalance.Credit)]
    public void Account_normal_balance_follows_type(AccountType type, NormalBalance expected) =>
        Account.NormalBalanceFor(type).Should().Be(expected);
}
