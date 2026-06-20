using Accountrack.SharedKernel.Domain;

namespace Accountrack.Purchasing.Domain;

/// <summary>
/// A purchase invoice (supplier bill — procure-to-pay slice 2). Posting it clears the GR/IR accrual
/// for what was received, records VAT input, and raises the payable: Dr GR/IR + Dr VAT Input / Cr AP
/// control, plus an AP subledger open item — all atomically (docs/POSTING_RULES.md, ACCOUNTING_DESIGN §5).
/// </summary>
public sealed class PurchaseInvoice : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<PurchaseInvoiceLine> _lines = new();

    private PurchaseInvoice() { }

    private PurchaseInvoice(
        string number, string? supplierInvoiceNo, Guid purchaseOrderId, Guid supplierId, string currency,
        DateOnly invoiceDate, DateOnly dueDate, string? notes)
    {
        Number = number;
        SupplierInvoiceNo = supplierInvoiceNo;
        PurchaseOrderId = purchaseOrderId;
        SupplierId = supplierId;
        Currency = currency;
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        Notes = notes;
    }

    public string Number { get; private set; } = default!;

    /// <summary>The supplier's own invoice reference (external document number).</summary>
    public string? SupplierInvoiceNo { get; private set; }

    public Guid PurchaseOrderId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string? Notes { get; private set; }

    public decimal SubTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }

    /// <summary>The GL journal posted for this invoice (Dr GR-IR + Dr VAT Input / Cr AP).</summary>
    public Guid? JournalEntryId { get; private set; }

    /// <summary>The AP subledger open item raised for this invoice.</summary>
    public Guid? ApOpenItemId { get; private set; }

    public IReadOnlyList<PurchaseInvoiceLine> Lines => _lines;

    public static PurchaseInvoice Create(
        string number, string? supplierInvoiceNo, Guid purchaseOrderId, Guid supplierId, string currency,
        DateOnly invoiceDate, DateOnly dueDate, string? notes) =>
        new(number, supplierInvoiceNo?.Trim(), purchaseOrderId, supplierId, currency, invoiceDate, dueDate, notes?.Trim());

    public void AddLine(Guid purchaseOrderLineId, Guid productId, decimal quantity, decimal unitPrice, decimal taxRate)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Invoice line quantity must be positive.");
        }

        _lines.Add(PurchaseInvoiceLine.Create(purchaseOrderLineId, productId, quantity, unitPrice, taxRate));
        Recalculate();
    }

    public void SetPosting(Guid journalEntryId, Guid apOpenItemId)
    {
        JournalEntryId = journalEntryId;
        ApOpenItemId = apOpenItemId;
    }

    /// <summary>Records a returned quantity against one invoice line (debit note — BR-PUR-7).</summary>
    public void ReturnLine(Guid invoiceLineId, decimal quantity)
    {
        var line = _lines.FirstOrDefault(l => l.Id == invoiceLineId)
            ?? throw new InvalidOperationException("Purchase-invoice line not found.");

        line.Return(quantity);
    }

    private void Recalculate()
    {
        SubTotal = Math.Round(_lines.Sum(l => l.LineNet), 4, MidpointRounding.ToEven);
        TaxTotal = Math.Round(_lines.Sum(l => l.LineTax), 4, MidpointRounding.ToEven);
        GrandTotal = SubTotal + TaxTotal;
    }
}

/// <summary>A purchase-invoice line: net = quantity × unit price, plus VAT at the snapshotted rate.</summary>
public sealed class PurchaseInvoiceLine : Entity
{
    private PurchaseInvoiceLine() { }

    private PurchaseInvoiceLine(Guid purchaseOrderLineId, Guid productId, decimal quantity, decimal unitPrice, decimal taxRate)
    {
        PurchaseOrderLineId = purchaseOrderLineId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
        LineNet = Math.Round(quantity * unitPrice, 4, MidpointRounding.ToEven);
        LineTax = Math.Round(LineNet * taxRate, 4, MidpointRounding.ToEven);
        LineTotal = LineNet + LineTax;
    }

    public Guid PurchaseInvoiceId { get; private set; }
    public Guid PurchaseOrderLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxRate { get; private set; }
    public decimal LineNet { get; private set; }
    public decimal LineTax { get; private set; }
    public decimal LineTotal { get; private set; }

    /// <summary>Cumulative quantity returned to the supplier via debit notes (BR-PUR-7).</summary>
    public decimal ReturnedQuantity { get; private set; }

    /// <summary>Quantity still eligible to be returned on this line.</summary>
    public decimal ReturnableQuantity => Quantity - ReturnedQuantity;

    internal void Return(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Returned quantity must be positive.");
        }

        if (quantity > ReturnableQuantity)
        {
            throw new InvalidOperationException("Returned quantity exceeds the invoiced, not-yet-returned quantity.");
        }

        ReturnedQuantity += quantity;
    }

    internal static PurchaseInvoiceLine Create(Guid purchaseOrderLineId, Guid productId, decimal quantity, decimal unitPrice, decimal taxRate) =>
        new(purchaseOrderLineId, productId, quantity, unitPrice, taxRate);
}
