using Accountrack.Application.Abstractions.Context;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Application.Services;
using Accountrack.Inventory.Domain;
using Accountrack.Modules.Contracts.Company;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Inventory.UnitTests;

public class InventoryLedgerServiceTests
{
    private static readonly DateOnly Date = new(2026, 6, 13);
    private readonly IStockBucketRepository _buckets = Substitute.For<IStockBucketRepository>();
    private readonly IInventoryTransactionRepository _txns = Substitute.For<IInventoryTransactionRepository>();
    private readonly ICompanyDirectory _companies = Substitute.For<ICompanyDirectory>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly Guid _product = Guid.NewGuid();
    private readonly Guid _warehouse = Guid.NewGuid();

    private InventoryLedgerService Service(bool allowNegative = false)
    {
        _companies.GetBoolSettingAsync(Arg.Any<Guid>(), CompanySettingKeys.AllowNegativeStock, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(allowNegative);
        return new(_buckets, _txns, _companies, _tenant);
    }

    [Fact]
    public async Task Receive_creates_a_bucket_and_records_a_transaction()
    {
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns((StockCostBucket?)null);

        var result = await Service().ReceiveAsync(
            _product, _warehouse, "IDR", 10m, 100m, Date, MovementType.Receipt, MovementSource.Manual, null, null,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RunningQtyAfter.Should().Be(10m);
        result.Value.RunningAvgCostAfter.Should().Be(100m);
        result.Value.CostApplied.Should().Be(1000m);
        _buckets.Received(1).Add(Arg.Any<StockCostBucket>());
        _txns.Received(1).Add(Arg.Any<InventoryTransaction>());
    }

    [Fact]
    public async Task Receive_into_existing_bucket_updates_the_average()
    {
        var bucket = StockCostBucket.Create(_product, _warehouse, "IDR");
        bucket.Receive(10m, 100m); // existing: 10 @ 100
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);

        var result = await Service().ReceiveAsync(
            _product, _warehouse, "IDR", 10m, 120m, Date, MovementType.Receipt, MovementSource.Manual, null, null,
            CancellationToken.None);

        result.Value.RunningQtyAfter.Should().Be(20m);
        result.Value.RunningAvgCostAfter.Should().Be(110m);
        _buckets.DidNotReceive().Add(Arg.Any<StockCostBucket>()); // reused existing
    }

    [Fact]
    public async Task Issue_returns_cost_at_average_and_reduces_on_hand()
    {
        var bucket = StockCostBucket.Create(_product, _warehouse, "IDR");
        bucket.Receive(20m, 110m); // 20 @ 110
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);

        var result = await Service().IssueAsync(
            _product, _warehouse, 5m, Date, MovementType.Issue, MovementSource.Sales, null, null,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CostApplied.Should().Be(550m);
        result.Value.RunningQtyAfter.Should().Be(15m);
        _txns.Received(1).Add(Arg.Any<InventoryTransaction>());
    }

    [Fact]
    public async Task Issue_fails_when_insufficient_stock_and_negative_disallowed()
    {
        var bucket = StockCostBucket.Create(_product, _warehouse, "IDR");
        bucket.Receive(5m, 100m);
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);

        var result = await Service().IssueAsync(
            _product, _warehouse, 10m, Date, MovementType.Issue, MovementSource.Sales, null, null,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVENTORY.INSUFFICIENT_STOCK");
        _txns.DidNotReceive().Add(Arg.Any<InventoryTransaction>());
    }

    [Fact]
    public async Task Issue_below_zero_is_allowed_when_the_company_setting_permits_it()
    {
        var bucket = StockCostBucket.Create(_product, _warehouse, "IDR");
        bucket.Receive(5m, 100m);
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);

        var result = await Service(allowNegative: true).IssueAsync(
            _product, _warehouse, 10m, Date, MovementType.Issue, MovementSource.Sales, null, null,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RunningQtyAfter.Should().Be(-5m);
        _txns.Received(1).Add(Arg.Any<InventoryTransaction>());
    }

    [Fact]
    public async Task Issue_with_no_bucket_fails()
    {
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns((StockCostBucket?)null);

        var result = await Service().IssueAsync(
            _product, _warehouse, 1m, Date, MovementType.Issue, MovementSource.Sales, null, null,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVENTORY.INSUFFICIENT_STOCK");
    }
}
