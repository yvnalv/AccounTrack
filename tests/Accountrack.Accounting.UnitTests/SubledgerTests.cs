using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Application.Features;
using Accountrack.Accounting.Application.Services;
using Accountrack.Accounting.Domain;
using Accountrack.SharedKernel.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Accounting.UnitTests;

public class SubledgerTests
{
    private const string Idr = "IDR";

    private static SubledgerOpenItem NewInvoice(decimal amount, DateOnly? due = null) =>
        SubledgerOpenItem.Open(
            SubledgerType.Receivable, Guid.NewGuid(), JournalSource.SalesInvoice, Guid.NewGuid(),
            "INV-1", new DateOnly(2026, 6, 1), due ?? new DateOnly(2026, 6, 30), Money.Create(amount, Idr));

    [Fact]
    public void Partial_allocation_reduces_outstanding_and_marks_partially_paid()
    {
        var item = NewInvoice(1_000_000m);

        item.Allocate("PAY-1", new DateOnly(2026, 6, 15), Money.Create(400_000m, Idr));

        item.OutstandingAmount.Amount.Should().Be(600_000m);
        item.SettledAmount.Amount.Should().Be(400_000m);
        item.Status.Should().Be(OpenItemStatus.PartiallyPaid);
        item.Allocations.Should().ContainSingle();
    }

    [Fact]
    public void Full_allocation_settles_the_item()
    {
        var item = NewInvoice(1_000_000m);

        item.Allocate("PAY-1", new DateOnly(2026, 6, 15), Money.Create(600_000m, Idr));
        item.Allocate("PAY-2", new DateOnly(2026, 6, 20), Money.Create(400_000m, Idr));

        item.OutstandingAmount.Amount.Should().Be(0m);
        item.Status.Should().Be(OpenItemStatus.Settled);
    }

    [Fact]
    public void Over_allocation_is_rejected()
    {
        var item = NewInvoice(1_000_000m);

        var act = () => item.Allocate("PAY-1", new DateOnly(2026, 6, 15), Money.Create(1_000_001m, Idr));

        act.Should().Throw<InvalidOperationException>().WithMessage("*exceeds*");
    }

    [Fact]
    public void Open_rejects_non_positive_amount()
    {
        var act = () => SubledgerOpenItem.Open(
            SubledgerType.Payable, Guid.NewGuid(), JournalSource.PurchaseInvoice, null,
            "BILL-1", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), Money.Create(0m, Idr));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Service_translates_over_allocation_into_a_business_error()
    {
        var item = NewInvoice(100_000m);
        var repo = Substitute.For<ISubledgerRepository>();
        repo.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        var result = await new SubledgerService(repo)
            .AllocateAsync(item.Id, "PAY-1", new DateOnly(2026, 6, 15), 150_000m, null, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ACCOUNTING.ALLOCATION_EXCEEDS_OUTSTANDING");
    }

    [Fact]
    public async Task Aging_buckets_open_items_by_days_past_due()
    {
        var party = Guid.NewGuid();
        var asOf = new DateOnly(2026, 6, 30);
        var items = new List<SubledgerOpenItem>
        {
            MakeFor(party, 100_000m, due: new DateOnly(2026, 7, 10)),  // not due  -> Current
            MakeFor(party, 200_000m, due: new DateOnly(2026, 6, 20)),  // 10 days  -> 1-30
            MakeFor(party, 300_000m, due: new DateOnly(2026, 5, 15)),  // 46 days  -> 31-60
            MakeFor(party, 400_000m, due: new DateOnly(2026, 1, 1)),   // 180 days -> 90+
        };

        var repo = Substitute.For<ISubledgerRepository>();
        repo.ListAsync(SubledgerType.Receivable, null, false, Arg.Any<CancellationToken>()).Returns(items);

        var result = await new GetAgingQueryHandler(repo)
            .Handle(new GetAgingQuery(SubledgerType.Receivable, asOf), CancellationToken.None);

        var report = result.Value;
        report.Total.Should().Be(1_000_000m);
        var row = report.Rows.Should().ContainSingle().Subject;
        row.Current.Should().Be(100_000m);
        row.Days1To30.Should().Be(200_000m);
        row.Days31To60.Should().Be(300_000m);
        row.Days61To90.Should().Be(0m);
        row.Days90Plus.Should().Be(400_000m);
    }

    private static SubledgerOpenItem MakeFor(Guid party, decimal amount, DateOnly due) =>
        SubledgerOpenItem.Open(
            SubledgerType.Receivable, party, JournalSource.SalesInvoice, Guid.NewGuid(),
            "INV", new DateOnly(2026, 1, 1), due, Money.Create(amount, Idr));
}
