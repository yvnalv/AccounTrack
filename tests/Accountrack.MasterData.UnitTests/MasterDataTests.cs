using Accountrack.MasterData.Application.Abstractions;
using Accountrack.MasterData.Application.Features;
using Accountrack.MasterData.Domain;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.MasterData.UnitTests;

public class DomainTests
{
    [Fact]
    public void Codes_are_normalized_to_uppercase()
    {
        UnitOfMeasure.Create("pcs", "Piece").Code.Should().Be("PCS");
        Warehouse.Create("main-wh", "Main").Code.Should().Be("MAIN-WH");
        Customer.Create("cust-01", "Acme", null, 30, 0).Code.Should().Be("CUST-01");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public void Tax_rate_must_be_a_fraction(decimal rate) =>
        FluentActions.Invoking(() => TaxCode.Create("X", "X", rate)).Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Tax_rate_accepts_valid_fraction() =>
        TaxCode.Create("PPN11", "PPN 11%", 0.11m).Rate.Should().Be(0.11m);

    [Fact]
    public void Customer_rejects_negative_payment_terms() =>
        FluentActions.Invoking(() => Customer.Create("C", "C", null, -1, 0))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void New_product_is_stock_tracked_sellable_purchasable_active()
    {
        var p = Product.Create("SKU1", "Widget", Guid.NewGuid(), null);
        p.IsStockTracked.Should().BeTrue();
        p.IsSold.Should().BeTrue();
        p.IsPurchased.Should().BeTrue();
        p.IsActive.Should().BeTrue();
        p.Code.Should().Be("SKU1");
    }
}

public class CreateProductHandlerTests
{
    private readonly ICodedRepository<Product> _products = Substitute.For<ICodedRepository<Product>>();
    private readonly ICodedRepository<UnitOfMeasure> _uoms = Substitute.For<ICodedRepository<UnitOfMeasure>>();
    private readonly IMasterDataUnitOfWork _uow = Substitute.For<IMasterDataUnitOfWork>();

    private CreateProductHandler Handler() => new(_products, _uoms, _uow);

    private static CreateProductCommand Cmd(string code = "SKU1") =>
        new(code, "Widget", Guid.NewGuid(), null, true, true, true);

    [Fact]
    public async Task Creates_a_product_when_code_is_unique_and_uom_exists()
    {
        _products.CodeExistsAsync("SKU1", Arg.Any<CancellationToken>()).Returns(false);
        _uoms.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().Handle(Cmd(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _products.Received(1).Add(Arg.Is<Product>(p => p.Code == "SKU1"));
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_duplicate_product_code()
    {
        _products.CodeExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Handler().Handle(Cmd(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MASTERDATA.PRODUCT_CODE_EXISTS");
        _products.DidNotReceive().Add(Arg.Any<Product>());
    }

    [Fact]
    public async Task Rejects_when_base_uom_does_not_exist()
    {
        _products.CodeExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _uoms.ExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await Handler().Handle(Cmd(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MasterDataErrors.UomNotFound);
    }
}
