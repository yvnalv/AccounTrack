using Accountrack.Expenses.Application;
using Accountrack.Expenses.Application.Abstractions;
using Accountrack.Expenses.Application.Features;
using Accountrack.Expenses.Domain;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Events;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Expenses.UnitTests;

public class ApprovalDecidedConsumerTests
{
    private static readonly DateOnly Date = new(2026, 6, 20);
    private static readonly Guid CashAccount = Guid.NewGuid();
    private static readonly Guid ExpenseAccount = Guid.NewGuid();

    private sealed class DirectUnitOfWork : ICrossModuleUnitOfWork
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct) =>
            work(ct);
    }

    private readonly IExpenseVoucherRepository _vouchers = Substitute.For<IExpenseVoucherRepository>();
    private readonly IExpensesUnitOfWork _expensesUow = Substitute.For<IExpensesUnitOfWork>();
    private readonly IGeneralLedgerPoster _ledger = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();
    private readonly ISubledgerPosting _subledger = Substitute.For<ISubledgerPosting>();

    private ApprovalDecidedConsumer Consumer() =>
        new(_vouchers, new DirectUnitOfWork(), _expensesUow,
            new ExpenseVoucherPoster(_ledger, _accounts, _subledger));

    private static ExpenseVoucher PendingVoucher()
    {
        var v = ExpenseVoucher.CreatePaid("EXP/1", Date, "PLN", CashAccount, "IDR", null, null);
        v.AddLine(Guid.NewGuid(), "EXPENSE.ELECTRICITY", "power", 500_000m, 0m);
        v.MarkPendingApproval(Guid.NewGuid());
        return v;
    }

    private static ApprovalDecided Decided(Guid docId, bool approved, string status) =>
        new(ExpenseDocumentTypes.ExpenseVoucher, docId, Guid.NewGuid(), status, Guid.NewGuid(), Guid.NewGuid(), approved);

    [Fact]
    public async Task Approval_posts_the_pending_voucher()
    {
        var v = PendingVoucher();
        _vouchers.GetByIdAsync(v.Id, Arg.Any<CancellationToken>()).Returns(v);
        _accounts.ResolveAsync("Expense", "EXPENSE.ELECTRICITY", Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(ExpenseAccount));
        LedgerPostingRequest? posted = null;
        _ledger.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { posted = ci.Arg<LedgerPostingRequest>(); return Result.Success(Guid.NewGuid()); });

        await Consumer().HandleAsync(Decided(v.Id, approved: true, status: "Approved"), CancellationToken.None);

        v.Status.Should().Be(ExpenseVoucherStatus.Posted);
        v.JournalEntryId.Should().NotBeNull();
        posted!.Lines.Should().Contain(l => l.AccountId == ExpenseAccount && l.Debit == 500_000m);
        posted.Lines.Should().Contain(l => l.AccountId == CashAccount && l.Credit == 500_000m);
    }

    [Fact]
    public async Task Rejection_marks_the_voucher_rejected_without_posting()
    {
        var v = PendingVoucher();
        _vouchers.GetByIdAsync(v.Id, Arg.Any<CancellationToken>()).Returns(v);

        await Consumer().HandleAsync(Decided(v.Id, approved: false, status: "Rejected"), CancellationToken.None);

        v.Status.Should().Be(ExpenseVoucherStatus.Rejected);
        v.JournalEntryId.Should().BeNull();
        await _expensesUow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_already_posted_voucher_is_not_posted_again()
    {
        var v = PendingVoucher();
        v.MarkPosted(Guid.NewGuid()); // already posted
        _vouchers.GetByIdAsync(v.Id, Arg.Any<CancellationToken>()).Returns(v);

        await Consumer().HandleAsync(Decided(v.Id, approved: true, status: "Approved"), CancellationToken.None);

        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_decision_for_another_document_type_is_ignored()
    {
        var e = new ApprovalDecided("PurchaseOrder", Guid.NewGuid(), Guid.NewGuid(), "Approved", Guid.NewGuid(), Guid.NewGuid(), true);

        await Consumer().HandleAsync(e, CancellationToken.None);

        await _vouchers.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
