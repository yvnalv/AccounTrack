using Accountrack.SharedKernel.Domain;

namespace Accountrack.Sales.Domain;

/// <summary>
/// A sales return / credit note (order-to-cash — returns). Posting it reverses billing for the
/// returned quantities (Dr Revenue + Dr VAT Output / Cr AR control), restocks the goods at their
/// original delivered cost (Dr Inventory / Cr COGS), and reduces the customer's receivable — all
/// atomically (docs/POSTING_RULES.md, BR-SAL-8). Immutable once posted.
/// </summary>
public sealed class SalesReturn : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<SalesReturnLine> _lines = new();

    private SalesReturn() { }

    private SalesReturn(
        string number, Guid salesInvoiceId, Guid salesOrderId, Guid customerId, Guid warehouseId,
        string currency, DateOnly returnDate, string? notes)
    {
        Number = number;
        SalesInvoiceId = salesInvoiceId;
        SalesOrderId = salesOrderId;
        CustomerId = customerId;
        WarehouseId = warehouseId;
        Currency = currency;
        ReturnDate = returnDate;
        Notes = notes;
    }

    public string Number { get; private set; } = default!;
    public Guid SalesInvoiceId { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateOnly ReturnDate { get; private set; }
    public string? Notes { get; private set; }

    public decimal SubTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }

    /// <summary>The GL journal posted for this return (credit note + restock).</summary>
    public Guid? JournalEntryId { get; private set; }

    /// <summary>Total cost of goods returned to stock (the COGS reversal amount).</summary>
    public decimal TotalCost => _lines.Sum(l => l.LineCost);

    public IReadOnlyList<SalesReturnLine> Lines => _lines;

    public static SalesReturn Create(
        string number, Guid salesInvoiceId, Guid salesOrderId, Guid customerId, Guid warehouseId,
        string currency, DateOnly returnDate, string? notes) =>
        new(number, salesInvoiceId, salesOrderId, customerId, warehouseId, currency, returnDate, notes?.Trim());

    public void AddLine(
        Guid salesInvoiceLineId, Guid salesOrderLineId, Guid productId,
        decimal quantity, decimal unitPrice, decimal taxRate, decimal unitCost)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Return line quantity must be positive.");
        }

        _lines.Add(SalesReturnLine.Create(
            salesInvoiceLineId, salesOrderLineId, productId, quantity, unitPrice, taxRate, unitCost));
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

/// <summary>A sales-return line: quantity credited at the invoice price/tax and restocked at cost.</summary>
public sealed class SalesReturnLine : Entity
{
    private SalesReturnLine() { }

    private SalesReturnLine(
        Guid salesInvoiceLineId, Guid salesOrderLineId, Guid productId,
        decimal quantity, decimal unitPrice, decimal taxRate, decimal unitCost)
    {
        SalesInvoiceLineId = salesInvoiceLineId;
        SalesOrderLineId = salesOrderLineId;
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

    public Guid SalesReturnId { get; private set; }
    public Guid SalesInvoiceLineId { get; private set; }
    public Guid SalesOrderLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxRate { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal LineNet { get; private set; }
    public decimal LineTax { get; private set; }
    public decimal LineTotal { get; private set; }
    public decimal LineCost { get; private set; }

    internal static SalesReturnLine Create(
        Guid salesInvoiceLineId, Guid salesOrderLineId, Guid productId,
        decimal quantity, decimal unitPrice, decimal taxRate, decimal unitCost) =>
        new(salesInvoiceLineId, salesOrderLineId, productId, quantity, unitPrice, taxRate, unitCost);
}
