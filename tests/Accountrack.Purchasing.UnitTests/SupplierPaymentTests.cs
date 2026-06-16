using Accountrack.Application.Abstractions.Context;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Application.Features;
using Accountrack.Purchasing.Domain;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Purchasing.UnitTests;

public class SupplierPaymentTests
{
    private static readonly DateOnly Date = new(2026, 6, 16);
    private static readonly Guid Supplier = Guid.NewGuid();
    private static readonly Guid CashAccount = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private sealed class DirectUnitOfWork : ICrossModuleUnitOfWork
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct) =>
            work(ct);
    }

    private readonly ISupplierPaymentRepository _payments = Substitute.For<ISupplierPaymentRepository>();
    private readonly IGeneralLedgerPoster _ledger = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();
    private readonly ISubledgerPosting _subledger = Substitute.For<ISubledgerPosting>();
    private readonly ICompanyDirectory _companies = Substitute.For<ICompanyDirectory>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();

    private LedgerPostingRequest? _posted;

    public SupplierPaymentTests()
    {
        _tenant.CompanyId.Returns(CompanyId);
        _companies.GetAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyInfo(CompanyId, "DEV", "IDR", 1));
        _accounts.ResolveAsync("SupplierPayment", PostingKeys.ApControl, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));
        _ledger.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { _posted = ci.Arg<LedgerPostingRequest>(); return Result.Success(Guid.NewGuid()); });
    }

    private PostSupplierPaymentHandler Handler() =>
        new(_payments, new DirectUnitOfWork(), _ledger, _accounts, _subledger, _companies, _tenant);

    // --- Domain ---

    [Fact]
    public void Total_is_the_sum_of_allocations_and_zero_amounts_are_rejected()
    {
        var payment = SupplierPayment.Create("PMT/202606/00001", Supplier, CashAccount, "IDR", Date, null, null);
        payment.AddAllocation(Guid.NewGuid(), 600m);
        payment.AddAllocation(Guid.NewGuid(), 400m);
        payment.TotalAmount.Should().Be(1000m);

        var act = () => payment.AddAllocation(Guid.NewGuid(), 0m);
        act.Should().Throw<InvalidOperationException>();
    }

    // --- Handler orchestration ---

    [Fact]
    public async Task Posting_a_payment_books_dr_ap_cr_cash_and_allocates_each_open_item()
    {
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        _subledger.AllocateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));

        SupplierPayment? captured = null;
        _payments.When(r => r.Add(Arg.Any<SupplierPayment>())).Do(ci => captured = ci.Arg<SupplierPayment>());

        var result = await Handler().Handle(
            new PostSupplierPaymentCommand(Supplier, CashAccount, Date, "TRX-1", null,
                new[]
                {
                    new SupplierPaymentAllocationInput(itemA, 600m),
                    new SupplierPaymentAllocationInput(itemB, 400m),
                }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.TotalAmount.Should().Be(1000m);

        // Dr AP 1000 (carries supplier) / Cr Cash 1000, balanced.
        _posted!.Lines.Sum(l => l.Debit).Should().Be(1000m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(1000m);
        _posted.Lines.Should().Contain(l => l.Debit == 1000m && l.SubledgerPartyId == Supplier);
        _posted.Lines.Should().Contain(l => l.Credit == 1000m && l.AccountId == CashAccount);

        // Each open item allocated its share.
        await _subledger.Received(1).AllocateAsync(itemA, Arg.Any<string>(), Date, 600m, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _subledger.Received(1).AllocateAsync(itemB, Arg.Any<string>(), Date, 400m, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_over_allocation_from_the_subledger_fails_the_payment()
    {
        var item = Guid.NewGuid();
        _subledger.AllocateAsync(item, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Error.BusinessRule("BR-ACC-7", "Allocation exceeds the outstanding amount.", "ACCOUNTING.ALLOCATION_EXCEEDS_OUTSTANDING"));

        var result = await Handler().Handle(
            new PostSupplierPaymentCommand(Supplier, CashAccount, Date, null, null,
                new[] { new SupplierPaymentAllocationInput(item, 5000m) }), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNTING.ALLOCATION_EXCEEDS_OUTSTANDING");
    }
}
