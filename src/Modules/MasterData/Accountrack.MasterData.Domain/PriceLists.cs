using Accountrack.SharedKernel.Domain;

namespace Accountrack.MasterData.Domain;

/// <summary>Whether a price list feeds Sales (sell prices) or Purchasing (buy prices).</summary>
public enum PriceListType
{
    Sales = 0,
    Purchase = 1,
}

/// <summary>
/// A named set of per-product prices for either Sales or Purchasing (ADR-0035). One list per type may
/// be the company <see cref="IsDefault"/>; customers/suppliers may point at a specific list that
/// overrides the default. Prices are in the company functional currency (one per company, ADR-0013).
/// </summary>
public sealed class PriceList : TenantOwnedEntity, IAggregateRoot
{
    private PriceList() { }

    private PriceList(string name, PriceListType type, bool isDefault)
    {
        Name = name;
        Type = type;
        IsDefault = isDefault;
        IsActive = true;
    }

    public string Name { get; private set; } = default!;
    public PriceListType Type { get; private set; }

    /// <summary>The fallback list for its <see cref="Type"/> when a party has no specific assignment.</summary>
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    public static PriceList Create(string name, PriceListType type, bool isDefault = false) =>
        new(name.Trim(), type, isDefault);

    public void Update(string name) => Name = name.Trim();

    public void SetDefault(bool isDefault) => IsDefault = isDefault;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
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
