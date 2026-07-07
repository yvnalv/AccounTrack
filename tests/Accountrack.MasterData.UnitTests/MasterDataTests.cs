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

public class MasterDataEditTests
{
    [Fact]
    public void Customer_update_changes_mutable_fields()
    {
        var c = Customer.Create("C1", "Old", null, 30, 0);
        c.Update("New Name", "01.234", 14, 5_000_000m, null);

        c.Name.Should().Be("New Name");
        c.TaxId.Should().Be("01.234");
        c.PaymentTermDays.Should().Be(14);
        c.CreditLimit.Should().Be(5_000_000m);
        c.Code.Should().Be("C1"); // natural key is immutable
    }

    [Fact]
    public void Activate_and_deactivate_toggle_is_active()
    {
        var w = Warehouse.Create("WH1", "Main");
        w.IsActive.Should().BeTrue();
        w.Deactivate();
        w.IsActive.Should().BeFalse();
        w.Activate();
        w.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Product_update_keeps_code_and_base_uom_immutable()
    {
        var uom = Guid.NewGuid();
        var p = Product.Create("SKU1", "Widget", uom, null);
        p.Update("Renamed", Guid.NewGuid(), isStockTracked: false, isSold: false, isPurchased: true,
            salePrice: null, purchasePrice: null);

        p.Name.Should().Be("Renamed");
        p.IsStockTracked.Should().BeFalse();
        p.IsSold.Should().BeFalse();
        p.IsPurchased.Should().BeTrue();
        p.Code.Should().Be("SKU1");
        p.BaseUomId.Should().Be(uom);
    }
}

public class UpdateCustomerHandlerTests
{
    private readonly ICodedRepository<Customer> _repo = Substitute.For<ICodedRepository<Customer>>();
    private readonly IMasterDataUnitOfWork _uow = Substitute.For<IMasterDataUnitOfWork>();

    [Fact]
    public async Task Returns_not_found_when_customer_missing()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Customer?)null);

        var result = await new UpdateCustomerHandler(_repo, _uow)
            .Handle(new UpdateCustomerCommand(Guid.NewGuid(), "X", null, 30, 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MASTERDATA.CUSTOMER_NOT_FOUND");
    }

    [Fact]
    public async Task Updates_and_saves_when_found()
    {
        var customer = Customer.Create("C1", "Old", null, 30, 0);
        _repo.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);

        var result = await new UpdateCustomerHandler(_repo, _uow)
            .Handle(new UpdateCustomerCommand(customer.Id, "New", "NPWP", 7, 1000), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        customer.Name.Should().Be("New");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_sets_expected_version_for_optimistic_concurrency_when_supplied()
    {
        var customer = Customer.Create("C1", "Old", null, 30, 0);
        _repo.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);
        var version = new byte[] { 7, 7 };

        var result = await new UpdateCustomerHandler(_repo, _uow)
            .Handle(new UpdateCustomerCommand(customer.Id, "New", null, 7, 0, null, version), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.Received(1).SetExpectedVersion(customer, version);
    }

    [Fact]
    public async Task Set_active_deactivates_when_false()
    {
        var customer = Customer.Create("C1", "Old", null, 30, 0);
        _repo.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer);

        await new SetCustomerActiveHandler(_repo, _uow)
            .Handle(new SetCustomerActiveCommand(customer.Id, false), CancellationToken.None);

        customer.IsActive.Should().BeFalse();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public class CustomerImportTests
{
    private readonly ICodedRepository<Customer> _repo = Substitute.For<ICodedRepository<Customer>>();
    private readonly IMasterDataUnitOfWork _uow = Substitute.For<IMasterDataUnitOfWork>();

    private void Existing(params Customer[] customers) =>
        _repo.ListAsync(Arg.Any<CancellationToken>()).Returns(customers);

    private const string Header = "Code,Name,TaxId,PaymentTermDays,CreditLimit\n";

    [Fact]
    public async Task Preview_classifies_create_update_and_error_rows()
    {
        Existing(Customer.Create("CUST-001", "Acme", null, 30, 0));
        var csv = Header +
            "CUST-001,Acme Updated,,30,0\n" +   // update
            "CUST-002,New Co,,15,500\n" +        // create
            ",Missing Code,,30,0\n";             // error

        var result = await new PreviewCustomerImportHandler(_repo)
            .Handle(new PreviewCustomerImportQuery(csv), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var p = result.Value;
        p.TotalRows.Should().Be(3);
        p.ToCreate.Should().Be(1);
        p.ToUpdate.Should().Be(1);
        p.ErrorRows.Should().Be(1);
    }

    [Fact]
    public async Task Commit_creates_and_updates_when_all_rows_are_valid()
    {
        var existing = Customer.Create("CUST-001", "Acme", null, 30, 0);
        Existing(existing);
        var csv = Header + "CUST-001,Acme Renamed,,45,1000\nCUST-002,New Co,,15,500\n";

        var result = await new CommitCustomerImportHandler(_repo, _uow)
            .Handle(new CommitCustomerImportCommand(csv), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Committed.Should().BeTrue();
        result.Value.Created.Should().Be(1);
        result.Value.Updated.Should().Be(1);
        existing.Name.Should().Be("Acme Renamed");
        _repo.Received(1).Add(Arg.Is<Customer>(c => c.Code == "CUST-002"));
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Commit_is_all_or_nothing_when_a_row_is_invalid()
    {
        Existing();
        var csv = Header + "CUST-002,New Co,,15,500\n,No Code,,30,0\n";

        var result = await new CommitCustomerImportHandler(_repo, _uow)
            .Handle(new CommitCustomerImportCommand(csv), CancellationToken.None);

        result.Value.Committed.Should().BeFalse();
        result.Value.ErrorRows.Should().Be(1);
        _repo.DidNotReceive().Add(Arg.Any<Customer>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public class SupplierWarehouseImportTests
{
    private readonly ICodedRepository<Supplier> _suppliers = Substitute.For<ICodedRepository<Supplier>>();
    private readonly ICodedRepository<Warehouse> _warehouses = Substitute.For<ICodedRepository<Warehouse>>();
    private readonly IMasterDataUnitOfWork _uow = Substitute.For<IMasterDataUnitOfWork>();

    [Fact]
    public async Task Supplier_commit_creates_and_updates_by_code()
    {
        var existing = Supplier.Create("SUP-001", "Globex", null, 30);
        _suppliers.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { existing });
        var csv = "Code,Name,TaxId,PaymentTermDays\nSUP-001,Globex Renamed,,45\nSUP-002,New Supplier,,20\n";

        var result = await new CommitSupplierImportHandler(_suppliers, _uow)
            .Handle(new CommitSupplierImportCommand(csv), CancellationToken.None);

        result.Value.Committed.Should().BeTrue();
        result.Value.Created.Should().Be(1);
        result.Value.Updated.Should().Be(1);
        existing.Name.Should().Be("Globex Renamed");
        existing.PaymentTermDays.Should().Be(45);
    }

    [Fact]
    public async Task Warehouse_preview_flags_missing_name()
    {
        _warehouses.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Warehouse>());
        var csv = "Code,Name,Address\nWH-001,,Jakarta\n";

        var result = await new PreviewWarehouseImportHandler(_warehouses)
            .Handle(new PreviewWarehouseImportQuery(csv), CancellationToken.None);

        result.Value.ErrorRows.Should().Be(1);
        result.Value.Rows[0].Errors.Should().Contain("Name is required.");
    }
}

public class ProductImportTests
{
    private readonly ICodedRepository<Product> _products = Substitute.For<ICodedRepository<Product>>();
    private readonly ICodedRepository<UnitOfMeasure> _uoms = Substitute.For<ICodedRepository<UnitOfMeasure>>();
    private readonly ICodedRepository<ProductCategory> _categories = Substitute.For<ICodedRepository<ProductCategory>>();
    private readonly IMasterDataUnitOfWork _uow = Substitute.For<IMasterDataUnitOfWork>();

    [Fact]
    public async Task Resolves_uom_and_category_by_code_and_creates()
    {
        var pcs = UnitOfMeasure.Create("PCS", "Piece");
        var cat = ProductCategory.Create("GENERAL", "General");
        _products.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Product>());
        _uoms.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { pcs });
        _categories.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { cat });
        var csv = "Code,Name,BaseUom,Category,StockTracked,Sold,Purchased\nSKU-1,Widget,PCS,GENERAL,true,true,false\n";

        Product? added = null;
        _products.When(r => r.Add(Arg.Any<Product>())).Do(ci => added = ci.Arg<Product>());

        var result = await new CommitProductImportHandler(_products, _uoms, _categories, _uow)
            .Handle(new CommitProductImportCommand(csv), CancellationToken.None);

        result.Value.Committed.Should().BeTrue();
        result.Value.Created.Should().Be(1);
        added!.BaseUomId.Should().Be(pcs.Id);
        added.CategoryId.Should().Be(cat.Id);
        added.IsPurchased.Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_uom_is_an_error_row()
    {
        _products.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Product>());
        _uoms.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<UnitOfMeasure>());
        _categories.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<ProductCategory>());
        var csv = "Code,Name,BaseUom,Category,StockTracked,Sold,Purchased\nSKU-1,Widget,NOPE,,true,true,true\n";

        var result = await new PreviewProductImportHandler(_products, _uoms, _categories)
            .Handle(new PreviewProductImportQuery(csv), CancellationToken.None);

        result.Value.ErrorRows.Should().Be(1);
        result.Value.Rows[0].Errors.Should().Contain(e => e.Contains("BaseUom"));
    }
}

public class MasterDataEditCompletionTests
{
    private readonly IMasterDataUnitOfWork _uow = Substitute.For<IMasterDataUnitOfWork>();

    [Fact]
    public void New_uom_and_category_are_active_and_editable()
    {
        var uom = UnitOfMeasure.Create("kg", "Kilogram");
        uom.IsActive.Should().BeTrue();
        uom.Update("Kilograms");
        uom.Name.Should().Be("Kilograms");
        uom.Deactivate();
        uom.IsActive.Should().BeFalse();
        uom.Activate();
        uom.IsActive.Should().BeTrue();

        var cat = ProductCategory.Create("raw", "Raw materials");
        cat.IsActive.Should().BeTrue();
        cat.Update("Raw mats");
        cat.Name.Should().Be("Raw mats");
    }

    [Fact]
    public void TaxCode_update_changes_name_and_rate_but_validates_fraction()
    {
        var tax = TaxCode.Create("PPN11", "PPN 11%", 0.11m);
        tax.Update("PPN 12%", 0.12m);
        tax.Name.Should().Be("PPN 12%");
        tax.Rate.Should().Be(0.12m);
        FluentActions.Invoking(() => tax.Update("Bad", 2m)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task UpdateUom_renames_existing_unit()
    {
        var repo = Substitute.For<ICodedRepository<UnitOfMeasure>>();
        var uom = UnitOfMeasure.Create("PCS", "Piece");
        repo.GetByIdAsync(uom.Id, Arg.Any<CancellationToken>()).Returns(uom);

        var result = await new UpdateUomHandler(repo, _uow).Handle(new UpdateUomCommand(uom.Id, "Pieces"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        uom.Name.Should().Be("Pieces");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetTaxCodeActive_deactivates_existing_code()
    {
        var repo = Substitute.For<ICodedRepository<TaxCode>>();
        var tax = TaxCode.Create("PPN11", "PPN 11%", 0.11m);
        repo.GetByIdAsync(tax.Id, Arg.Any<CancellationToken>()).Returns(tax);

        var result = await new SetTaxCodeActiveHandler(repo, _uow).Handle(new SetTaxCodeActiveCommand(tax.Id, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tax.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateCategory_returns_not_found_for_missing_id()
    {
        var repo = Substitute.For<ICodedRepository<ProductCategory>>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProductCategory?)null);

        var result = await new UpdateCategoryHandler(repo, _uow).Handle(new UpdateCategoryCommand(Guid.NewGuid(), "X"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
