using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Features;
using Accountrack.Accounting.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Accounting.UnitTests;

public class ChartOfAccountsEditTests
{
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IAccountingReadStore _readStore = Substitute.For<IAccountingReadStore>();
    private readonly IAccountingUnitOfWork _uow = Substitute.For<IAccountingUnitOfWork>();

    private SetAccountActiveCommandHandler ActiveHandler() => new(_accounts, _readStore, _uow);

    private void NoActivity() =>
        _readStore.GetAccountMovementsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(System.Array.Empty<AccountMovementRow>());

    [Fact]
    public async Task Update_renames_and_toggles_posting()
    {
        var acc = Account.CreateWithId(Guid.NewGuid(), "6500", "Misc", AccountType.Expense);
        _accounts.GetByIdAsync(acc.Id, Arg.Any<CancellationToken>()).Returns(acc);

        var result = await new UpdateAccountCommandHandler(_accounts, _uow)
            .Handle(new UpdateAccountCommand(acc.Id, "Miscellaneous", AllowPosting: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        acc.Name.Should().Be("Miscellaneous");
        acc.AllowPosting.Should().BeFalse();
    }

    [Fact]
    public async Task Update_sets_expected_version_for_optimistic_concurrency_when_supplied()
    {
        var acc = Account.CreateWithId(Guid.NewGuid(), "6500", "Misc", AccountType.Expense);
        _accounts.GetByIdAsync(acc.Id, Arg.Any<CancellationToken>()).Returns(acc);
        var version = new byte[] { 4, 2 };

        var result = await new UpdateAccountCommandHandler(_accounts, _uow)
            .Handle(new UpdateAccountCommand(acc.Id, "Miscellaneous", AllowPosting: true, RowVersion: version), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _accounts.Received(1).SetExpectedVersion(acc, version);
    }

    [Fact]
    public async Task Deactivate_a_plain_unused_account_succeeds()
    {
        var acc = Account.CreateWithId(Guid.NewGuid(), "6500", "Misc", AccountType.Expense);
        _accounts.GetByIdAsync(acc.Id, Arg.Any<CancellationToken>()).Returns(acc);
        NoActivity();

        var result = await ActiveHandler().Handle(new SetAccountActiveCommand(acc.Id, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        acc.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_a_system_account_is_rejected()
    {
        var acc = Account.CreateWithId(Guid.NewGuid(), "1200", "Inventory", AccountType.Asset, isSystem: true);
        _accounts.GetByIdAsync(acc.Id, Arg.Any<CancellationToken>()).Returns(acc);
        NoActivity();

        var result = await ActiveHandler().Handle(new SetAccountActiveCommand(acc.Id, false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNTING.ACCOUNT_IS_SYSTEM");
        acc.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_an_account_with_activity_is_rejected()
    {
        var acc = Account.CreateWithId(Guid.NewGuid(), "6500", "Misc", AccountType.Expense);
        _accounts.GetByIdAsync(acc.Id, Arg.Any<CancellationToken>()).Returns(acc);
        _readStore.GetAccountMovementsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(new[] { new AccountMovementRow(acc.Id, 500_000m, 0m) });

        var result = await ActiveHandler().Handle(new SetAccountActiveCommand(acc.Id, false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNTING.ACCOUNT_IN_USE");
    }
}
