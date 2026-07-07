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
    private readonly ICodedRepository<Product> _products = Substitute.For<ICodedRepository<Product>>();

    private static readonly Guid Uom = Guid.NewGuid();

    private ResolvePricesHandler Handler() => new(_lists, _customers, _suppliers, _products);

    private Product Product(string code, decimal? salePrice) =>
        Domain.Product.Create(code, code, Uom, null, salePrice: salePrice);

    private Customer CustomerWithList(Guid listId)
    {
        var c = Customer.Create("C1", "Acme", null, 30, 0m);
        c.Update("Acme", null, 30, 0m, listId);
        return c;
    }

    [Fact]
    public async Task No_party_list_returns_empty_so_the_form_uses_the_product_base_price()
    {
        var customer = Customer.Create("C1", "Acme", null, 30, 0m); // no list assigned
        _customers.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);

        var result = await Handler().Handle(new ResolvePricesQuery(PriceListType.Sales, customer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Discount_list_applies_a_percentage_off_the_product_base_price()
    {
        var list = PriceList.Create("Wholesale", PriceListType.Sales, discountPercent: 10m);
        var customer = CustomerWithList(list.Id);
        var widget = Product("W", 100m);
        var gadget = Product("G", 250m);

        _customers.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);
        _lists.GetAsync(list.Id, Arg.Any<CancellationToken>()).Returns(list);
        _lists.GetItemsAsync(list.Id, Arg.Any<CancellationToken>()).Returns(new List<PriceListItem>());
        _products.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<Product> { widget, gadget });

        var result = await Handler().Handle(new ResolvePricesQuery(PriceListType.Sales, customer.Id), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new Dictionary<Guid, decimal>
        {
            [widget.Id] = 90m,  // 100 − 10%
            [gadget.Id] = 225m, // 250 − 10%
        });
    }

    [Fact]
    public async Task A_product_override_wins_over_the_discount()
    {
        var list = PriceList.Create("Wholesale", PriceListType.Sales, discountPercent: 10m);
        var customer = CustomerWithList(list.Id);
        var widget = Product("W", 100m);

        _customers.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);
        _lists.GetAsync(list.Id, Arg.Any<CancellationToken>()).Returns(list);
        _lists.GetItemsAsync(list.Id, Arg.Any<CancellationToken>())
            .Returns(new List<PriceListItem> { PriceListItem.Create(list.Id, widget.Id, 80m) });
        _products.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<Product> { widget });

        var result = await Handler().Handle(new ResolvePricesQuery(PriceListType.Sales, customer.Id), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [widget.Id] = 80m }); // override, not 90
    }

    [Fact]
    public async Task Overrides_only_list_returns_just_the_overrides()
    {
        var list = PriceList.Create("Specials", PriceListType.Sales, discountPercent: 0m);
        var customer = CustomerWithList(list.Id);
        var widget = Product("W", 100m);
        var gadget = Product("G", 250m);

        _customers.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);
        _lists.GetAsync(list.Id, Arg.Any<CancellationToken>()).Returns(list);
        _lists.GetItemsAsync(list.Id, Arg.Any<CancellationToken>())
            .Returns(new List<PriceListItem> { PriceListItem.Create(list.Id, gadget.Id, 200m) });
        _products.ListAsync(Arg.Any<CancellationToken>()).Returns(new List<Product> { widget, gadget });

        var result = await Handler().Handle(new ResolvePricesQuery(PriceListType.Sales, customer.Id), CancellationToken.None);

        // widget has no override and the list has no discount → absent (form uses its base price)
        result.Value.Should().BeEquivalentTo(new Dictionary<Guid, decimal> { [gadget.Id] = 200m });
    }
}
