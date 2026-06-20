using Accountrack.SharedKernel.Domain;

namespace Accountrack.Purchasing.Domain;

/// <summary>
/// A purchase return / debit note (procure-to-pay — returns). Posting it removes the returned goods
/// from stock at moving-average cost (Cr Inventory), reverses the supplier billing (Dr AP control /
/// Cr VAT Input), books any cost-vs-price variance, and reduces the supplier payable — all
/// atomically (docs/POSTING_RULES.md, BR-PUR-7). Immutable once posted.
/// </summary>
public sealed class PurchaseReturn : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<PurchaseReturnLine> _lines = new();

    private PurchaseReturn() { }

    private PurchaseReturn(
        string number, Guid purchaseInvoiceId, Guid purchaseOrderId, Guid supplierId, Guid warehouseId,
        string currency, DateOnly returnDate, string? notes)
    {
        Number = number;
        PurchaseInvoiceId = purchaseInvoiceId;
        PurchaseOrderId = purchaseOrderId;
        SupplierId = supplierId;
        WarehouseId = warehouseId;
        Currency = currency;
        ReturnDate = returnDate;
        Notes = notes;
    }

    public string Number { get; private set; } = default!;
    public Guid PurchaseInvoiceId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateOnly ReturnDate { get; private set; }
    public string? Notes { get; private set; }

    public decimal SubTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }

    /// <summary>The GL journal posted for this return (debit note + de-stock).</summary>
    public Guid? JournalEntryId { get; private set; }

    /// <summary>Total inventory cost removed from stock (the Cr Inventory amount).</summary>
    public decimal TotalCost => _lines.Sum(l => l.LineCost);

    public IReadOnlyList<PurchaseReturnLine> Lines => _lines;

    public static PurchaseReturn Create(
        string number, Guid purchaseInvoiceId, Guid purchaseOrderId, Guid supplierId, Guid warehouseId,
        string currency, DateOnly returnDate, string? notes) =>
        new(number, purchaseInvoiceId, purchaseOrderId, supplierId, warehouseId, currency, returnDate, notes?.Trim());

    public void AddLine(
        Guid purchaseInvoiceLineId, Guid purchaseOrderLineId, Guid productId,
        decimal quantity, decimal unitPrice, decimal taxRate, decimal unitCost)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Return line quantity must be positive.");
        }

        _lines.Add(PurchaseReturnLine.Create(
            purchaseInvoiceLineId, purchaseOrderLineId, productId, quantity, unitPrice, taxRate, unitCost));
        Recalculate();
    }

    public void SetJournal(Guid journalEntryId) => JournalEntryId = journalEntryId;

    private void Recalculate()
    {
        SubTotal = Math.Round(_lines.Sum(l => l.LineNet), 4, MidpointRounding.ToEven);
        TaxTotal = Math.Round(_lines.Sum(l => l.LineTax), 4, MidpointRounding.ToEven);
        GrandTotal = SubTotal + TaxTotal;
    }
}

/// <summary>A purchase-return line: quantity debited at the invoice price/tax and de-stocked at cost.</summary>
public sealed class PurchaseReturnLine : Entity
{
    private PurchaseReturnLine() { }

    private PurchaseReturnLine(
        Guid purchaseInvoiceLineId, Guid purchaseOrderLineId, Guid productId,
        decimal quantity, decimal unitPrice, decimal taxRate, decimal unitCost)
    {
        PurchaseInvoiceLineId = purchaseInvoiceLineId;
        PurchaseOrderLineId = purchaseOrderLineId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
        UnitCost = unitCost;
        LineNet = Math.Round(quantity * unitPrice, 4, MidpointRounding.ToEven);
        LineTax = Math.Round(LineNet * taxRate, 4, MidpointRounding.ToEven);
        LineTotal = LineNet + LineTax;
        LineCost = Math.Round(quantity * unitCost, 4, MidpointRounding.ToEven);
    }

    public Guid PurchaseReturnId { get; private set; }
    public Guid PurchaseInvoiceLineId { get; private set; }
    public Guid PurchaseOrderLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxRate { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal LineNet { get; private set; }
    public decimal LineTax { get; private set; }
    public decimal LineTotal { get; private set; }
    public decimal LineCost { get; private set; }

    internal static PurchaseReturnLine Create(
        Guid purchaseInvoiceLineId, Guid purchaseOrderLineId, Guid productId,
        decimal quantity, decimal unitPrice, decimal taxRate, decimal unitCost) =>
        new(purchaseInvoiceLineId, purchaseOrderLineId, productId, quantity, unitPrice, taxRate, unitCost);
}
