using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Application.Features;
using Accountrack.MasterData.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.MasterData.UnitTests;

public class PriceListTests
{
    private readonly IPriceListRepository _lists = Substitute.For<IPriceListRepository>();
    private readonly ICodedRepository<Customer> _customers = Substitute.For<ICodedRepository<Customer>>();
    private readonly ICodedRepository<Supplier> _suppliers = Substitute.For<ICodedRepository<Supplier>>();

    private static readonly Guid P1 = Guid.NewGuid();
    private static readonly Guid P2 = Guid.NewGuid();
    private static readonly Guid P3 = Guid.NewGuid();

    private ResolvePricesHandler Handler() => new(_lists, _customers, _suppliers);

    [Fact]
    public async Task Resolves_default_prices_when_the_party_has_no_list()
    {
        var def = PriceList.Create("Standard", PriceListType.Sales, isDefault: true);
        _lists.GetDefaultAsync(PriceListType.Sales, Arg.Any<CancellationToken>()).Returns(def);
        _lists.GetItemsAsync(def.Id, Arg.Any<CancellationToken>()).Returns(new List<PriceListItem>
        {
            PriceListItem.Create(def.Id, P1, 100m),
            PriceListItem.Create(def.Id, P2, 200m),
        });

        var result = await Handler().Handle(new ResolvePricesQuery(PriceListType.Sales, PartyId: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [P1] = 100m, [P2] = 200m });
    }

    [Fact]
    public async Task Party_list_overrides_the_default_and_adds_its_own_products()
    {
        var def = PriceList.Create("Standard", PriceListType.Sales, isDefault: true);
        var vip = PriceList.Create("VIP", PriceListType.Sales, isDefault: false);

        var customer = Customer.Create("C1", "Acme", null, 30, 0m);
        customer.Update("Acme", null, 30, 0m, vip.Id); // assign the VIP list

        _lists.GetDefaultAsync(PriceListType.Sales, Arg.Any<CancellationToken>()).Returns(def);
        _lists.GetItemsAsync(def.Id, Arg.Any<CancellationToken>()).Returns(new List<PriceListItem>
        {
            PriceListItem.Create(def.Id, P1, 100m),
            PriceListItem.Create(def.Id, P2, 200m),
        });
        _customers.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);
        _lists.GetAsync(vip.Id, Arg.Any<CancellationToken>()).Returns(vip);
        _lists.GetItemsAsync(vip.Id, Arg.Any<CancellationToken>()).Returns(new List<PriceListItem>
        {
            PriceListItem.Create(vip.Id, P2, 250m), // overrides the default P2
            PriceListItem.Create(vip.Id, P3, 300m), // adds P3
        });

        var result = await Handler().Handle(
            new ResolvePricesQuery(PriceListType.Sales, customer.Id), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new Dictionary<Guid, decimal>
        {
            [P1] = 100m, // from default
            [P2] = 250m, // party overrides default
            [P3] = 300m, // party-only
        });
    }

    [Fact]
    public async Task Returns_empty_when_no_default_and_no_party_list()
    {
        _lists.GetDefaultAsync(PriceListType.Sales, Arg.Any<CancellationToken>()).Returns((PriceList?)null);

        var result = await Handler().Handle(new ResolvePricesQuery(PriceListType.Sales, PartyId: null), CancellationToken.None);

        result.Value.Should().BeEmpty();
    }
}
