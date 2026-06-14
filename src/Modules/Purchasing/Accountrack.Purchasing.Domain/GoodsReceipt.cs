using Accountrack.SharedKernel.Domain;

namespace Accountrack.Purchasing.Domain;

/// <summary>
/// A goods receipt: goods received against a purchase order (procure-to-pay — slice 2). Posting a
/// receipt writes the inventory ledger and a Dr Inventory / Cr GR-IR journal atomically
/// (docs/POSTING_RULES.md, INTEGRATION_EVENTS.md §2). Immutable once posted.
/// </summary>
public sealed class GoodsReceipt : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<GoodsReceiptLine> _lines = new();

    private GoodsReceipt() { }

    private GoodsReceipt(
        string number, Guid purchaseOrderId, Guid supplierId, Guid warehouseId, string currency,
        DateOnly receiptDate, string? notes)
    {
        Number = number;
        PurchaseOrderId = purchaseOrderId;
        SupplierId = supplierId;
        WarehouseId = warehouseId;
        Currency = currency;
        ReceiptDate = receiptDate;
        Notes = notes;
    }

    public string Number { get; private set; } = default!;
    public Guid PurchaseOrderId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateOnly ReceiptDate { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>The GL journal posted for this receipt (Dr Inventory / Cr GR-IR).</summary>
    public Guid? JournalEntryId { get; private set; }

    public IReadOnlyList<GoodsReceiptLine> Lines => _lines;

    public decimal TotalCost => _lines.Sum(l => l.LineCost);

    public static GoodsReceipt Create(
        string number, Guid purchaseOrderId, Guid supplierId, Guid warehouseId, string currency,
        DateOnly receiptDate, string? notes) =>
        new(number, purchaseOrderId, supplierId, warehouseId, currency, receiptDate, notes?.Trim());

    public void AddLine(Guid purchaseOrderLineId, Guid productId, decimal quantity, decimal unitCost)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Receipt line quantity must be positive.");
        }

        _lines.Add(GoodsReceiptLine.Create(purchaseOrderLineId, productId, quantity, unitCost));
    }

    public void SetJournal(Guid journalEntryId) => JournalEntryId = journalEntryId;
}

/// <summary>A goods-receipt line: quantity received at the purchase (moving-average) unit cost.</summary>
public sealed class GoodsReceiptLine : Entity
{
    private GoodsReceiptLine() { }

    private GoodsReceiptLine(Guid purchaseOrderLineId, Guid productId, decimal quantity, decimal unitCost)
    {
        PurchaseOrderLineId = purchaseOrderLineId;
        ProductId = productId;
        Quantity = quantity;
        UnitCost = unitCost;
        LineCost = Math.Round(quantity * unitCost, 4, MidpointRounding.ToEven);
    }

    public Guid GoodsReceiptId { get; private set; }
    public Guid PurchaseOrderLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal LineCost { get; private set; }

    internal static GoodsReceiptLine Create(Guid purchaseOrderLineId, Guid productId, decimal quantity, decimal unitCost) =>
        new(purchaseOrderLineId, productId, quantity, unitCost);
}
