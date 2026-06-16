using Accountrack.Application.Abstractions.Context;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Application.Features;
using Accountrack.Sales.Domain;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Sales.UnitTests;

public class CustomerPaymentTests
{
    private static readonly DateOnly Date = new(2026, 6, 16);
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid CashAccount = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private sealed class DirectUnitOfWork : ICrossModuleUnitOfWork
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct) =>
            work(ct);
    }

    private readonly ICustomerPaymentRepository _payments = Substitute.For<ICustomerPaymentRepository>();
    private readonly IGeneralLedgerPoster _ledger = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();
    private readonly ISubledgerPosting _subledger = Substitute.For<ISubledgerPosting>();
    private readonly ICompanyDirectory _companies = Substitute.For<ICompanyDirectory>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();

    private LedgerPostingRequest? _posted;

    public CustomerPaymentTests()
    {
        _tenant.CompanyId.Returns(CompanyId);
        _companies.GetAsync(CompanyId, Arg.Any<CancellationToken>()).Returns(new CompanyInfo(CompanyId, "DEV", "IDR", 1));
        _accounts.ResolveAsync("CustomerPayment", PostingKeys.ArControl, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));
        _ledger.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { _posted = ci.Arg<LedgerPostingRequest>(); return Result.Success(Guid.NewGuid()); });
    }

    private PostCustomerPaymentHandler Handler() =>
        new(_payments, new DirectUnitOfWork(), _ledger, _accounts, _subledger, _companies, _tenant);

    [Fact]
    public void Total_is_the_sum_of_allocations_and_zero_amounts_are_rejected()
    {
        var payment = CustomerPayment.Create("RCT/202606/00001", Customer, CashAccount, "IDR", Date, null, null);
        payment.AddAllocation(Guid.NewGuid(), 1200m);
        payment.AddAllocation(Guid.NewGuid(), 800m);
        payment.TotalAmount.Should().Be(2000m);

        var act = () => payment.AddAllocation(Guid.NewGuid(), 0m);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Posting_a_payment_books_dr_cash_cr_ar_and_allocates_each_open_item()
    {
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        _subledger.AllocateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));

        CustomerPayment? captured = null;
        _payments.When(r => r.Add(Arg.Any<CustomerPayment>())).Do(ci => captured = ci.Arg<CustomerPayment>());

        var result = await Handler().Handle(
            new PostCustomerPaymentCommand(Customer, CashAccount, Date, "RCPT-1", null,
                new[]
                {
                    new CustomerPaymentAllocationInput(itemA, 1200m),
                    new CustomerPaymentAllocationInput(itemB, 800m),
                }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.TotalAmount.Should().Be(2000m);

        // Dr Cash 2000 / Cr AR 2000 (AR carries customer), balanced.
        _posted!.Lines.Sum(l => l.Debit).Should().Be(2000m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(2000m);
        _posted.Lines.Should().Contain(l => l.Debit == 2000m && l.AccountId == CashAccount);
        _posted.Lines.Should().Contain(l => l.Credit == 2000m && l.SubledgerPartyId == Customer);
        _posted.Source.Should().Be(LedgerSource.CustomerPayment);

        await _subledger.Received(1).AllocateAsync(itemA, Arg.Any<string>(), Date, 1200m, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _subledger.Received(1).AllocateAsync(itemB, Arg.Any<string>(), Date, 800m, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_over_allocation_from_the_subledger_fails_the_payment()
    {
        var item = Guid.NewGuid();
        _subledger.AllocateAsync(item, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<decimal>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Error.BusinessRule("BR-ACC-7", "Allocation exceeds the outstanding amount.", "ACCOUNTING.ALLOCATION_EXCEEDS_OUTSTANDING"));

        var result = await Handler().Handle(
            new PostCustomerPaymentCommand(Customer, CashAccount, Date, null, null,
                new[] { new CustomerPaymentAllocationInput(item, 9999m) }), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNTING.ALLOCATION_EXCEEDS_OUTSTANDING");
    }
}
