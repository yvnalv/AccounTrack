using Accountrack.Expenses.Application.Features;
using Accountrack.Expenses.Domain;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Expenses.UnitTests;

/// <summary>Covers the draft-lifecycle guards and the reversal posting added for full parity (BR-EXP-4/6).</summary>
public class ExpenseVoucherReversalTests
{
    private static readonly DateOnly Date = new(2026, 7, 2);
    private static readonly Guid CashAccount = Guid.NewGuid();

    private readonly IGeneralLedgerPoster _ledger = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();
    private readonly ISubledgerPosting _subledger = Substitute.For<ISubledgerPosting>();

    private LedgerPostingRequest? _posted;
    private readonly Guid _expenseAccount = Guid.NewGuid();
    private readonly Guid _apAccount = Guid.NewGuid();

    private ExpenseVoucherPoster Poster()
    {
        _accounts.ResolveAsync("Expense", "EXPENSE.E", Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(_expenseAccount));
        _accounts.ResolveAsync("Expense", PostingKeys.ApControl, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(_apAccount));
        _ledger.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { _posted = ci.Arg<LedgerPostingRequest>(); return Result.Success(Guid.NewGuid()); });
        return new ExpenseVoucherPoster(_ledger, _accounts, _subledger);
    }

    private static ExpenseVoucher PostedPaid()
    {
        var v = ExpenseVoucher.CreatePaid("EXP/1", Date, "PLN", CashAccount, "IDR", null, null);
        v.AddLine(Guid.NewGuid(), "EXPENSE.E", "elec", 500_000m, 0m);
        v.MarkPosted(Guid.NewGuid());
        return v;
    }

    [Fact]
    public void Cancel_and_edit_are_only_allowed_on_a_draft()
    {
        var posted = PostedPaid();
        posted.Invoking(v => v.Cancel()).Should().Throw<InvalidOperationException>();
        posted.Invoking(v => v.EditDraft(Date, "x", CashAccount, null, null, null, null))
            .Should().Throw<InvalidOperationException>();

        var draft = ExpenseVoucher.CreatePaid("EXP/2", Date, "PLN", CashAccount, "IDR", null, null);
        draft.Cancel();
        draft.Status.Should().Be(ExpenseVoucherStatus.Cancelled);
    }

    [Fact]
    public void MarkReversed_requires_a_posted_voucher()
    {
        var draft = ExpenseVoucher.CreatePaid("EXP/3", Date, "PLN", CashAccount, "IDR", null, null);
        draft.Invoking(v => v.MarkReversed(Guid.NewGuid())).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Reversing_a_paid_voucher_posts_a_mirror_journal_and_marks_it_reversed()
    {
        var voucher = PostedPaid();

        var result = await Poster().ReverseAsync(voucher, Date, "entered in error", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        voucher.Status.Should().Be(ExpenseVoucherStatus.Reversed);
        voucher.ReversalJournalEntryId.Should().NotBeNull();

        // Original was Dr Expense 500k / Cr Cash 500k → reversal is the exact mirror, still balanced.
        _posted!.Lines.Sum(l => l.Debit).Should().Be(500_000m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(500_000m);
        _posted.Lines.Should().Contain(l => l.AccountId == _expenseAccount && l.Credit == 500_000m);
        _posted.Lines.Should().Contain(l => l.AccountId == CashAccount && l.Debit == 500_000m);
    }

    [Fact]
    public async Task Reversing_an_on_account_voucher_with_payments_is_blocked()
    {
        var supplierId = Guid.NewGuid();
        var openItemId = Guid.NewGuid();
        var voucher = ExpenseVoucher.CreateOnAccount("EXP/4", Date, null, supplierId, Date.AddDays(30), "IDR", null, null);
        voucher.AddLine(Guid.NewGuid(), "EXPENSE.E", "rent", 500_000m, 0m);
        voucher.MarkPosted(Guid.NewGuid());
        voucher.SetApOpenItem(openItemId);

        // Outstanding < grand total → a payment has been allocated → reversal must be refused.
        _subledger.GetOutstandingAsync(openItemId, Arg.Any<CancellationToken>())
            .Returns(Result.Success(200_000m));

        var result = await Poster().ReverseAsync(voucher, Date, null, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EXPENSES.REVERSAL_HAS_PAYMENTS");
        voucher.Status.Should().Be(ExpenseVoucherStatus.Posted); // unchanged
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reversing_an_unpaid_on_account_voucher_settles_the_payable()
    {
        var supplierId = Guid.NewGuid();
        var openItemId = Guid.NewGuid();
        var voucher = ExpenseVoucher.CreateOnAccount("EXP/5", Date, null, supplierId, Date.AddDays(30), "IDR", null, null);
        voucher.AddLine(Guid.NewGuid(), "EXPENSE.E", "rent", 500_000m, 0m);
        voucher.MarkPosted(Guid.NewGuid());
        voucher.SetApOpenItem(openItemId);

        _subledger.GetOutstandingAsync(openItemId, Arg.Any<CancellationToken>())
            .Returns(Result.Success(500_000m)); // fully unpaid
        _subledger.AllocateAsync(openItemId, Arg.Any<string>(), Arg.Any<DateOnly>(), 500_000m, voucher.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));

        var result = await Poster().ReverseAsync(voucher, Date, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        voucher.Status.Should().Be(ExpenseVoucherStatus.Reversed);
        // Mirror journal debits AP; the open item is settled for its full outstanding amount.
        _posted!.Lines.Should().Contain(l => l.AccountId == _apAccount && l.Debit == 500_000m);
        await _subledger.Received(1).AllocateAsync(openItemId, Arg.Any<string>(), Date, 500_000m, voucher.Id, Arg.Any<CancellationToken>());
    }
}
