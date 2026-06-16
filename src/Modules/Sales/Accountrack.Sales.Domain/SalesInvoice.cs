using Accountrack.SharedKernel.Domain;

namespace Accountrack.Sales.Domain;

/// <summary>
/// A sales invoice (customer bill — order-to-cash slice 2). Posting it recognises revenue and VAT
/// and raises the receivable: Dr AR control / Cr Revenue + Cr VAT Output, plus an AR subledger open
/// item — all atomically (docs/POSTING_RULES.md, ACCOUNTING_DESIGN §5).
/// </summary>
public sealed class SalesInvoice : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<SalesInvoiceLine> _lines = new();

    private SalesInvoice() { }

    private SalesInvoice(
        string number, Guid salesOrderId, Guid customerId, string currency,
        DateOnly invoiceDate, DateOnly dueDate, string? notes)
    {
        Number = number;
        SalesOrderId = salesOrderId;
        CustomerId = customerId;
        Currency = currency;
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        Notes = notes;
    }

    public string Number { get; private set; } = default!;
    public Guid SalesOrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string? Notes { get; private set; }

    public decimal SubTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }

    /// <summary>The GL journal posted for this invoice (Dr AR / Cr Revenue + VAT Output).</summary>
    public Guid? JournalEntryId { get; private set; }

    /// <summary>The AR subledger open item raised for this invoice.</summary>
    public Guid? ArOpenItemId { get; private set; }

    public IReadOnlyList<SalesInvoiceLine> Lines => _lines;

    public static SalesInvoice Create(
        string number, Guid salesOrderId, Guid customerId, string currency,
        DateOnly invoiceDate, DateOnly dueDate, string? notes) =>
        new(number, salesOrderId, customerId, currency, invoiceDate, dueDate, notes?.Trim());

    public void AddLine(Guid salesOrderLineId, Guid productId, decimal quantity, decimal unitPrice, decimal taxRate)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Invoice line quantity must be positive.");
        }

        _lines.Add(SalesInvoiceLine.Create(salesOrderLineId, productId, quantity, unitPrice, taxRate));
        Recalculate();
    }

    public void SetPosting(Guid journalEntryId, Guid arOpenItemId)
    {
        JournalEntryId = journalEntryId;
        ArOpenItemId = arOpenItemId;
    }

    private void Recalculate()
    {
        SubTotal = Math.Round(_lines.Sum(l => l.LineNet), 4, MidpointRounding.ToEven);
        TaxTotal = Math.Round(_lines.Sum(l => l.LineTax), 4, MidpointRounding.ToEven);
        GrandTotal = SubTotal + TaxTotal;
    }
}

/// <summary>A sales-invoice line: net = quantity × unit price, plus VAT at the snapshotted rate.</summary>
public sealed class SalesInvoiceLine : Entity
{
    private SalesInvoiceLine() { }

    private SalesInvoiceLine(Guid salesOrderLineId, Guid productId, decimal quantity, decimal unitPrice, decimal taxRate)
    {
        SalesOrderLineId = salesOrderLineId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
        LineNet = Math.Round(quantity * unitPrice, 4, MidpointRounding.ToEven);
        LineTax = Math.Round(LineNet * taxRate, 4, MidpointRounding.ToEven);
        LineTotal = LineNet + LineTax;
    }

    public Guid SalesInvoiceId { get; private set; }
    public Guid SalesOrderLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxRate { get; private set; }
    public decimal LineNet { get; private set; }
    public decimal LineTax { get; private set; }
    public decimal LineTotal { get; private set; }

    internal static SalesInvoiceLine Create(Guid salesOrderLineId, Guid productId, decimal quantity, decimal unitPrice, decimal taxRate) =>
        new(salesOrderLineId, productId, quantity, unitPrice, taxRate);
}
