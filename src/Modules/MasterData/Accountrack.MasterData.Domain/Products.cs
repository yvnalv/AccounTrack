using Accountrack.SharedKernel.Domain;

namespace Accountrack.MasterData.Domain;

/// <summary>A unit a product is tracked/traded in (e.g. PCS, KG, BOX).</summary>
public sealed class UnitOfMeasure : TenantOwnedEntity, IAggregateRoot, IHasCode
{
    private UnitOfMeasure() { }

    private UnitOfMeasure(Guid id, string code, string name) : base(id)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    public static UnitOfMeasure Create(string code, string name) =>
        new(Guid.NewGuid(), code.Trim().ToUpperInvariant(), name.Trim());
}

/// <summary>A grouping for products.</summary>
public sealed class ProductCategory : TenantOwnedEntity, IAggregateRoot, IHasCode
{
    private ProductCategory() { }

    private ProductCategory(Guid id, string code, string name) : base(id)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    public static ProductCategory Create(string code, string name) =>
        new(Guid.NewGuid(), code.Trim().ToUpperInvariant(), name.Trim());
}

/// <summary>
/// A product/item. Stock is tracked via the Inventory ledger when <see cref="IsStockTracked"/> is
/// true (Product never stores authoritative stock — ADR-0014).
/// </summary>
public sealed class Product : TenantOwnedEntity, IAggregateRoot, IHasCode
{
    private Product() { }

    private Product(string code, string name, Guid baseUomId, Guid? categoryId)
    {
        Code = code;
        Name = name;
        BaseUomId = baseUomId;
        CategoryId = categoryId;
        IsStockTracked = true;
        IsSold = true;
        IsPurchased = true;
        IsActive = true;
    }

    /// <summary>The product code / SKU (unique per company).</summary>
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public Guid BaseUomId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public bool IsStockTracked { get; private set; }
    public bool IsSold { get; private set; }
    public bool IsPurchased { get; private set; }
    public bool IsActive { get; private set; }

    public static Product Create(
        string code, string name, Guid baseUomId, Guid? categoryId,
        bool isStockTracked = true, bool isSold = true, bool isPurchased = true)
    {
        var product = new Product(code.Trim().ToUpperInvariant(), name.Trim(), baseUomId, categoryId)
        {
            IsStockTracked = isStockTracked,
            IsSold = isSold,
            IsPurchased = isPurchased,
        };
        return product;
    }

    public void Rename(string name) => Name = name.Trim();

    /// <summary>
    /// Edits the mutable fields. Code and base UoM are immutable after creation (the base UoM
    /// underpins inventory costing — changing it would corrupt historical valuation).
    /// </summary>
    public void Update(string name, Guid? categoryId, bool isStockTracked, bool isSold, bool isPurchased)
    {
        Name = name.Trim();
        CategoryId = categoryId;
        IsStockTracked = isStockTracked;
        IsSold = isSold;
        IsPurchased = isPurchased;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
