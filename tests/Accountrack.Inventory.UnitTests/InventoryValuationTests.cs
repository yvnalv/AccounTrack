using Accountrack.Application.Abstractions.Context;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Application.Features;
using Accountrack.Inventory.Domain;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Inventory.UnitTests;

public class InventoryValuationTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ProductA = Guid.NewGuid();
    private static readonly Guid ProductB = Guid.NewGuid();

    private readonly IStockBucketRepository _buckets = Substitute.For<IStockBucketRepository>();
    private readonly IMasterDataLookup _lookup = Substitute.For<IMasterDataLookup>();
    private readonly IGeneralLedgerBalances _gl = Substitute.For<IGeneralLedgerBalances>();
    private readonly ICompanyDirectory _companies = Substitute.For<ICompanyDirectory>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();

    public InventoryValuationTests()
    {
        _tenant.CompanyId.Returns(CompanyId);
        _companies.GetAsync(CompanyId, Arg.Any<CancellationToken>()).Returns(new CompanyInfo(CompanyId, "MAIN", "IDR", 1));
        var names = new Dictionary<Guid, string> { [ProductA] = "Widget", [ProductB] = "Gadget" };
        _lookup.ResolveNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ci => (IReadOnlyDictionary<Guid, string>)names);

        var aWh1 = StockCostBucket.Create(ProductA, Guid.NewGuid(), "IDR"); aWh1.Receive(10m, 100m);  // 1,000
        var aWh2 = StockCostBucket.Create(ProductA, Guid.NewGuid(), "IDR"); aWh2.Receive(5m, 100m);    //   500
        var bWh1 = StockCostBucket.Create(ProductB, Guid.NewGuid(), "IDR"); bWh1.Receive(2m, 250m);    //   500
        _buckets.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<StockCostBucket> { aWh1, aWh2, bWh1 });
    }

    private GetInventoryValuationHandler Handler() => new(_buckets, _lookup, _gl, _companies, _tenant);

    [Fact]
    public async Task Aggregates_value_by_product_and_reconciles_to_the_GL()
    {
        _gl.GetInventoryControlBalanceAsync(Arg.Any<CancellationToken>()).Returns(2_000m);

        var v = (await Handler().Handle(new GetInventoryValuationQuery(), CancellationToken.None)).Value;

        v.TotalValue.Should().Be(2_000m);
        v.Rows.Should().HaveCount(2);
        v.Rows[0].ProductName.Should().Be("Widget");        // ordered by value desc
        v.Rows[0].Quantity.Should().Be(15m);
        v.Rows[0].AvgUnitCost.Should().Be(100m);
        v.Rows[0].Value.Should().Be(1_500m);
        v.GlInventoryBalance.Should().Be(2_000m);
        v.Difference.Should().Be(0m);
        v.IsReconciled.Should().BeTrue();
    }

    [Fact]
    public async Task Flags_a_difference_when_the_GL_disagrees()
    {
        _gl.GetInventoryControlBalanceAsync(Arg.Any<CancellationToken>()).Returns(1_950m);

        var v = (await Handler().Handle(new GetInventoryValuationQuery(), CancellationToken.None)).Value;

        v.Difference.Should().Be(50m);
        v.IsReconciled.Should().BeFalse();
    }
}
