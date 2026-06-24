using Accountrack.Application.Abstractions.Context;
using Accountrack.Expenses.Application.Abstractions;
using Accountrack.Expenses.Application.Features;
using Accountrack.Expenses.Domain;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Approval;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Expenses.UnitTests;

public class ExpenseVoucherTests
{
    private static readonly DateOnly Date = new(2026, 6, 20);
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid CashAccount = Guid.NewGuid();

    private sealed class DirectUnitOfWork : ICrossModuleUnitOfWork
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct) =>
            work(ct);
    }

    private readonly IExpenseCategoryRepository _categories = Substitute.For<IExpenseCategoryRepository>();
    private readonly IExpenseVoucherRepository _vouchers = Substitute.For<IExpenseVoucherRepository>();
    private readonly IGeneralLedgerPoster _ledger = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();
    private readonly ISubledgerPosting _subledger = Substitute.For<ISubledgerPosting>();
    private readonly IMasterDataLookup _masterData = Substitute.For<IMasterDataLookup>();
    private readonly ICompanyDirectory _companies = Substitute.For<ICompanyDirectory>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IExpensesUnitOfWork _expensesUow = Substitute.For<IExpensesUnitOfWork>();
    private readonly IApprovalService _approval = Substitute.For<IApprovalService>();

    private LedgerPostingRequest? _posted;

    private PostExpenseVoucherHandler Handler() =>
        new(_categories, _vouchers, new DirectUnitOfWork(), _expensesUow,
            new ExpenseVoucherPoster(_ledger, _accounts, _subledger), _approval, _masterData, _companies, _tenant);

    private (Guid electricity, Guid transport, Guid elecAccount, Guid transAccount) Setup()
    {
        _tenant.CompanyId.Returns(CompanyId);
        _companies.GetAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyInfo(CompanyId, "MAIN", "IDR", 1));

        var elec = ExpenseCategory.Create("ELECTRICITY", "Electricity", "EXPENSE.ELECTRICITY");
        var trans = ExpenseCategory.Create("TRANSPORT", "Transport", "EXPENSE.TRANSPORT");
        _categories.GetByIdAsync(elec.Id, Arg.Any<CancellationToken>()).Returns(elec);
        _categories.GetByIdAsync(trans.Id, Arg.Any<CancellationToken>()).Returns(trans);

        var elecAccount = Guid.NewGuid();
        var transAccount = Guid.NewGuid();
        var vatAccount = Guid.NewGuid();
        _accounts.ResolveAsync("Expense", "EXPENSE.ELECTRICITY", Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(elecAccount));
        _accounts.ResolveAsync("Expense", "EXPENSE.TRANSPORT", Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(transAccount));
        _accounts.ResolveAsync("Expense", PostingKeys.VatInput, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(vatAccount));
        _ledger.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { _posted = ci.Arg<LedgerPostingRequest>(); return Result.Success(Guid.NewGuid()); });

        // Default: no approval rule matches → auto-approved → posts immediately.
        _approval.SubmitAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyDictionary<string, decimal>>(), Arg.Any<CancellationToken>())
            .Returns(new ApprovalSubmissionResult(Guid.NewGuid(), "AutoApproved"));

        return (elec.Id, trans.Id, elecAccount, transAccount);
    }

    [Fact]
    public void Voucher_totals_include_creditable_vat()
    {
        var elec = ExpenseCategory.Create("E", "E", "EXPENSE.E");
        var v = ExpenseVoucher.CreatePaid("EXP/1", Date, "PLN", CashAccount, "IDR", null, null);
        v.AddLine(elec.Id, elec.PostingRuleKey, "electricity", 500_000m, 0m);
        v.AddLine(elec.Id, elec.PostingRuleKey, "transport", 100_000m, 0.11m);

        v.SubTotal.Should().Be(600_000m);
        v.TaxTotal.Should().Be(11_000m);
        v.GrandTotal.Should().Be(611_000m);
        v.IsOnAccount.Should().BeFalse();
    }

    [Fact]
    public async Task Posting_debits_each_expense_account_and_vat_and_credits_cash()
    {
        var (elec, trans, elecAccount, transAccount) = Setup();

        ExpenseVoucher? captured = null;
        _vouchers.When(r => r.Add(Arg.Any<ExpenseVoucher>())).Do(ci => captured = ci.Arg<ExpenseVoucher>());

        var result = await Handler().Handle(
            new PostExpenseVoucherCommand(Date, "PLN & Grab", CashAccount, null, null, "JUN", null, new[]
            {
                new ExpenseVoucherLineInput(elec, "electricity", 500_000m, 0m),
                new ExpenseVoucherLineInput(trans, "transport", 100_000m, 0.11m),
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.GrandTotal.Should().Be(611_000m);

        // Dr Electricity 500k + Dr Transport 100k + Dr VAT 11k / Cr Cash 611k — balanced.
        _posted!.Source.Should().Be(LedgerSource.Expense);
        _posted.Lines.Sum(l => l.Debit).Should().Be(611_000m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(611_000m);
        _posted.Lines.Should().Contain(l => l.AccountId == elecAccount && l.Debit == 500_000m);
        _posted.Lines.Should().Contain(l => l.AccountId == transAccount && l.Debit == 100_000m);
        _posted.Lines.Should().Contain(l => l.AccountId == CashAccount && l.Credit == 611_000m);
    }

    [Fact]
    public async Task On_account_voucher_credits_ap_and_opens_a_payable()
    {
        var (elec, _, elecAccount, _) = Setup();
        var supplierId = Guid.NewGuid();
        var apControl = Guid.NewGuid();
        var dueDate = Date.AddDays(30);
        _masterData.SupplierExistsAsync(supplierId, Arg.Any<CancellationToken>()).Returns(true);
        _accounts.ResolveAsync("Expense", PostingKeys.ApControl, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(apControl));
        _subledger.OpenPayableAsync(
                supplierId, Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
                Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));

        ExpenseVoucher? captured = null;
        _vouchers.When(r => r.Add(Arg.Any<ExpenseVoucher>())).Do(ci => captured = ci.Arg<ExpenseVoucher>());

        var result = await Handler().Handle(
            new PostExpenseVoucherCommand(Date, "Office Rent", null, supplierId, dueDate, "RENT", null, new[]
            {
                new ExpenseVoucherLineInput(elec, "rent", 1_000_000m, 0m),
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.IsOnAccount.Should().BeTrue();
        captured.ApOpenItemId.Should().NotBeNull();

        // Dr Expense 1,000,000 / Cr AP 1,000,000 — credit carries the supplier as subledger party.
        _posted!.Lines.Should().Contain(l => l.AccountId == elecAccount && l.Debit == 1_000_000m);
        _posted.Lines.Should().Contain(l => l.AccountId == apControl && l.Credit == 1_000_000m && l.SubledgerPartyId == supplierId);
        _posted.Lines.Should().NotContain(l => l.AccountId == CashAccount);

        await _subledger.Received(1).OpenPayableAsync(
            supplierId, Arg.Any<Guid>(), Arg.Any<string>(), Date, dueDate, 1_000_000m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task On_account_voucher_with_unknown_supplier_is_rejected()
    {
        Setup();
        var supplierId = Guid.NewGuid();
        _masterData.SupplierExistsAsync(supplierId, Arg.Any<CancellationToken>()).Returns(false);

        var (elec, _, _, _) = Setup();
        var result = await Handler().Handle(
            new PostExpenseVoucherCommand(Date, null, null, supplierId, Date.AddDays(30), null, null, new[]
            {
                new ExpenseVoucherLineInput(elec, "x", 100m, 0m),
            }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EXPENSES.SUPPLIER_NOT_FOUND");
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Same_category_lines_collapse_into_one_debit()
    {
        var (elec, _, elecAccount, _) = Setup();

        var result = await Handler().Handle(
            new PostExpenseVoucherCommand(Date, null, CashAccount, null, null, null, null, new[]
            {
                new ExpenseVoucherLineInput(elec, "a", 300_000m, 0m),
                new ExpenseVoucherLineInput(elec, "b", 200_000m, 0m),
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _posted!.Lines.Count(l => l.AccountId == elecAccount).Should().Be(1);
        _posted.Lines.Single(l => l.AccountId == elecAccount).Debit.Should().Be(500_000m);
        _posted.Lines.Should().NotContain(l => l.Debit > 0 && l.AccountId != elecAccount); // no VAT line (0 tax)
    }

    [Fact]
    public async Task A_voucher_that_matches_an_approval_rule_waits_pending_and_does_not_post()
    {
        var (elec, _, _, _) = Setup();
        // A rule matches → engine returns Pending.
        _approval.SubmitAsync(ExpenseDocumentTypes.ExpenseVoucher, Arg.Any<Guid>(),
                Arg.Any<IReadOnlyDictionary<string, decimal>>(), Arg.Any<CancellationToken>())
            .Returns(new ApprovalSubmissionResult(Guid.NewGuid(), "Pending"));

        ExpenseVoucher? captured = null;
        _vouchers.When(r => r.Add(Arg.Any<ExpenseVoucher>())).Do(ci => captured = ci.Arg<ExpenseVoucher>());

        var result = await Handler().Handle(
            new PostExpenseVoucherCommand(Date, "Big spend", CashAccount, null, null, null, null, new[]
            {
                new ExpenseVoucherLineInput(elec, "big", 9_000_000m, 0m),
            }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.Status.Should().Be(ExpenseVoucherStatus.PendingApproval);
        captured.ApprovalRequestId.Should().NotBeNull();
        captured.JournalEntryId.Should().BeNull();
        await _expensesUow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_inactive_or_missing_category_is_rejected()
    {
        _tenant.CompanyId.Returns(CompanyId);
        _companies.GetAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyInfo(CompanyId, "MAIN", "IDR", 1));
        _categories.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ExpenseCategory?)null);

        var result = await Handler().Handle(
            new PostExpenseVoucherCommand(Date, null, CashAccount, null, null, null, null, new[]
            {
                new ExpenseVoucherLineInput(Guid.NewGuid(), "x", 100m, 0m),
            }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EXPENSES.LINE_CATEGORY_NOT_FOUND");
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }
}
