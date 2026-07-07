using Accountrack.SharedKernel.Domain;

namespace Accountrack.MasterData.Domain;

/// <summary>Whether a price list feeds Sales (sell prices) or Purchasing (buy prices).</summary>
public enum PriceListType
{
    Sales = 0,
    Purchase = 1,
}

/// <summary>
/// A shared pricing rule for Sales or Purchasing (ADR-0035): a percentage discount off the product's
/// base price (<c>Product.SalePrice</c>/<c>PurchasePrice</c>), plus optional per-product fixed-price
/// overrides (<see cref="PriceListItem"/>). Many customers/suppliers can point at one list (e.g.
/// "Wholesale −10%"); a party with no list simply gets the product base price. Amounts are in the
/// company functional currency (ADR-0013).
/// </summary>
public sealed class PriceList : TenantOwnedEntity, IAggregateRoot
{
    private const int PctScale = 4;

    private PriceList() { }

    private PriceList(string name, PriceListType type, decimal discountPercent)
    {
        Name = name;
        Type = type;
        DiscountPercent = Clamp(discountPercent);
        IsActive = true;
    }

    public string Name { get; private set; } = default!;
    public PriceListType Type { get; private set; }

    /// <summary>Percentage off the product base price applied to every product (0–100); per-product
    /// overrides win over it. 0 means "overrides only".</summary>
    public decimal DiscountPercent { get; private set; }
    public bool IsActive { get; private set; }

    public static PriceList Create(string name, PriceListType type, decimal discountPercent = 0m) =>
        new(name.Trim(), type, discountPercent);

    public void Update(string name, decimal discountPercent)
    {
        Name = name.Trim();
        DiscountPercent = Clamp(discountPercent);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static decimal Clamp(decimal pct) =>
        Math.Round(Math.Clamp(pct, 0m, 100m), PctScale, MidpointRounding.ToEven);
}

/// <summary>A single product's price within a <see cref="PriceList"/> (ADR-0035).</summary>
public sealed class PriceListItem : TenantOwnedEntity, IAggregateRoot
{
    private const int PriceScale = 4;

    private PriceListItem() { }

    private PriceListItem(Guid priceListId, Guid productId, decimal unitPrice)
    {
        PriceListId = priceListId;
        ProductId = productId;
        UnitPrice = unitPrice;
    }

    public Guid PriceListId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal UnitPrice { get; private set; }

    public static PriceListItem Create(Guid priceListId, Guid productId, decimal unitPrice) =>
        new(priceListId, productId, Math.Round(unitPrice, PriceScale, MidpointRounding.ToEven));

    public void SetPrice(decimal unitPrice) =>
        UnitPrice = Math.Round(unitPrice, PriceScale, MidpointRounding.ToEven);
}
