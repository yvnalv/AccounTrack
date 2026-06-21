using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Features;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Modules.Contracts.Company;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Accounting.UnitTests;

public class CloseFiscalYearTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Account Revenue = Account.CreateWithId(Guid.NewGuid(), "4000", "Sales Revenue", AccountType.Revenue);
    private static readonly Account Expense = Account.CreateWithId(Guid.NewGuid(), "5000", "COGS", AccountType.Expense);
    private static readonly Account Cash = Account.CreateWithId(Guid.NewGuid(), "1000", "Cash", AccountType.Asset);
    private static readonly Account Retained = Account.CreateWithId(Guid.NewGuid(), "3900", "Retained Earnings", AccountType.Equity);

    private readonly IFiscalPeriodRepository _periods = Substitute.For<IFiscalPeriodRepository>();
    private readonly IAccountingReadStore _store = Substitute.For<IAccountingReadStore>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IPostingRuleResolver _resolver = Substitute.For<IPostingRuleResolver>();
    private readonly IJournalPoster _poster = Substitute.For<IJournalPoster>();
    private readonly ICompanyDirectory _companies = Substitute.For<ICompanyDirectory>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IAccountingUnitOfWork _uow = Substitute.For<IAccountingUnitOfWork>();
    private JournalEntry? _posted;

    public CloseFiscalYearTests()
    {
        _tenant.CompanyId.Returns(CompanyId);
        _companies.GetAsync(CompanyId, Arg.Any<CancellationToken>())
            .Returns(new CompanyInfo(CompanyId, "MAIN", "IDR", 1));
        _accounts.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Account> { Revenue, Expense, Cash, Retained });
        _resolver.ResolveAsync(PostingRule.AnyEvent, PostingRuleKeys.RetainedEarnings, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Retained.Id));
        _poster.PostAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci => { _posted = ci.Arg<JournalEntry>(); return Result.Success(Guid.NewGuid()); });
    }

    private CloseFiscalYearCommandHandler Handler() =>
        new(_periods, _store, _accounts, _resolver, _poster, _companies, _tenant, _uow);

    [Fact]
    public async Task Close_year_posts_closing_entry_and_carries_net_income_to_retained_earnings()
    {
        var year = FiscalYear.Create(2026);
        _periods.GetFiscalYearByIdAsync(year.Id, Arg.Any<CancellationToken>()).Returns(year);
        _store.GetTrialBalanceAsync(Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new List<TrialBalanceRow>
            {
                new("4000", "Sales Revenue", "Revenue", 0m, 1_000_000m, -1_000_000m),
                new("5000", "COGS", "Expense", 600_000m, 0m, 600_000m),
                new("1000", "Cash", "Asset", 400_000m, 0m, 400_000m),
            });

        var result = await Handler().Handle(new CloseFiscalYearCommand(year.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NetIncome.Should().Be(400_000m);
        result.Value.JournalEntryId.Should().NotBeNull();

        _posted.Should().NotBeNull();
        _posted!.Source.Should().Be(JournalSource.PeriodClose);
        _posted.IsBalanced.Should().BeTrue();
        _posted.Lines.Should().HaveCount(3);                       // revenue, expense, retained earnings
        _posted.Lines.Single(l => l.AccountId == Revenue.Id).Debit.Amount.Should().Be(1_000_000m);
        _posted.Lines.Single(l => l.AccountId == Expense.Id).Credit.Amount.Should().Be(600_000m);
        _posted.Lines.Single(l => l.AccountId == Retained.Id).Credit.Amount.Should().Be(400_000m);

        year.IsClosed.Should().BeTrue();
        year.PeriodFor(year.EndDate)!.Status.Should().Be(FiscalPeriodStatus.Locked);
    }

    [Fact]
    public async Task Closing_an_already_closed_year_is_rejected()
    {
        var year = FiscalYear.Create(2026);
        year.Close();
        _periods.GetFiscalYearByIdAsync(year.Id, Arg.Any<CancellationToken>()).Returns(year);

        var result = await Handler().Handle(new CloseFiscalYearCommand(year.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNTING.FISCAL_YEAR_ALREADY_CLOSED");
        await _poster.DidNotReceive().PostAsync(Arg.Any<JournalEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Close_year_requires_the_final_period_to_be_open()
    {
        var year = FiscalYear.Create(2026);
        year.PeriodFor(year.EndDate)!.Close();   // close the final (December) period
        _periods.GetFiscalYearByIdAsync(year.Id, Arg.Any<CancellationToken>()).Returns(year);

        var result = await Handler().Handle(new CloseFiscalYearCommand(year.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNTING.FINAL_PERIOD_NOT_OPEN");
    }
}
