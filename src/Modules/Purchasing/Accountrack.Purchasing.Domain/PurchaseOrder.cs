using Accountrack.SharedKernel.Domain;

namespace Accountrack.Purchasing.Domain;

public enum PurchaseOrderStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    PartiallyReceived = 5,
    Received = 6,
}

/// <summary>
/// A purchase order (procure-to-pay — slice 1). Created as a draft, then submitted for approval;
/// its status is advanced by the Approval Workflow via integration events (Goods Receipt, invoicing
/// and GL posting are later slices).
/// </summary>
public sealed class PurchaseOrder : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<PurchaseOrderLine> _lines = new();

    private PurchaseOrder() { }

    private PurchaseOrder(string number, Guid supplierId, Guid warehouseId, string currency, DateOnly orderDate, string? notes)
    {
        Number = number;
        SupplierId = supplierId;
        WarehouseId = warehouseId;
        Currency = currency;
        OrderDate = orderDate;
        Notes = notes;
        Status = PurchaseOrderStatus.Draft;
    }

    public string Number { get; private set; } = default!;
    public Guid SupplierId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateOnly OrderDate { get; private set; }
    public string? Notes { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }

    public decimal SubTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }

    public IReadOnlyList<PurchaseOrderLine> Lines => _lines;

    public static PurchaseOrder CreateDraft(
        string number, Guid supplierId, Guid warehouseId, string currency, DateOnly orderDate, string? notes) =>
        new(number, supplierId, warehouseId, currency.Trim().ToUpperInvariant(), orderDate, notes?.Trim());

    public void AddLine(Guid productId, decimal quantity, decimal unitPrice, decimal taxRate, string? description)
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            throw new InvalidOperationException("Lines can only be changed on a draft purchase order.");
        }

        _lines.Add(PurchaseOrderLine.Create(productId, quantity, unitPrice, taxRate, description));
        Recalculate();
    }

    /// <summary>Submitted, waiting on approval.</summary>
    public void MarkPendingApproval(Guid approvalRequestId)
    {
        EnsureDraftWithLines();
        ApprovalRequestId = approvalRequestId;
        Status = PurchaseOrderStatus.PendingApproval;
    }

    /// <summary>Submitted and immediately approved (no approval rule matched).</summary>
    public void MarkAutoApproved(Guid approvalRequestId)
    {
        EnsureDraftWithLines();
        ApprovalRequestId = approvalRequestId;
        Status = PurchaseOrderStatus.Approved;
    }

    public void MarkApproved()
    {
        if (Status == PurchaseOrderStatus.PendingApproval)
        {
            Status = PurchaseOrderStatus.Approved;
        }
    }

    public void MarkRejected()
    {
        if (Status == PurchaseOrderStatus.PendingApproval)
        {
            Status = PurchaseOrderStatus.Rejected;
        }
    }

    public void Cancel()
    {
        if (Status is PurchaseOrderStatus.Approved or PurchaseOrderStatus.Rejected)
        {
            throw new InvalidOperationException("A decided purchase order cannot be cancelled.");
        }

        Status = PurchaseOrderStatus.Cancelled;
    }

    /// <summary>Whether goods can still be received against this order.</summary>
    public bool CanReceive =>
        Status is PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived;

    /// <summary>
    /// Records a received quantity against one line and advances the order's receipt status
    /// (BR-PUR-2). Throws if the order is not receivable or the quantity exceeds what's outstanding.
    /// </summary>
    public void ReceiveLine(Guid lineId, decimal quantity)
    {
        if (!CanReceive)
        {
            throw new InvalidOperationException("Goods can only be received against an approved purchase order.");
        }

        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException("Purchase-order line not found.");

        line.Receive(quantity);

        Status = _lines.All(l => l.IsFullyReceived)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;
    }

    /// <summary>
    /// Records an invoiced quantity against one line (BR-PUR-3). A line can only be invoiced up to
    /// what has been received (three-way match), so the GR/IR accrual is cleared by what was billed.
    /// </summary>
    public void InvoiceLine(Guid lineId, decimal quantity)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException("Purchase-order line not found.");

        line.Invoice(quantity);
    }

    private void EnsureDraftWithLines()
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft purchase order can be submitted.");
        }

        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("A purchase order requires at least one line.");
        }
    }

    private void Recalculate()
    {
        SubTotal = Math.Round(_lines.Sum(l => l.LineSubTotal), 4, MidpointRounding.ToEven);
        TaxTotal = Math.Round(_lines.Sum(l => l.LineTaxAmount), 4, MidpointRounding.ToEven);
        GrandTotal = SubTotal + TaxTotal;
    }
}

/// <summary>A purchase-order line: quantity × unit price, plus a snapshotted tax rate.</summary>
public sealed class PurchaseOrderLine : Entity
{
    private PurchaseOrderLine() { }

    private PurchaseOrderLine(Guid productId, decimal quantity, decimal unitPrice, decimal taxRate, string? description)
    {
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
        Description = description;
        LineSubTotal = Math.Round(quantity * unitPrice, 4, MidpointRounding.ToEven);
        LineTaxAmount = Math.Round(LineSubTotal * taxRate, 4, MidpointRounding.ToEven);
        LineTotal = LineSubTotal + LineTaxAmount;
    }

    public Guid PurchaseOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    /// <summary>Fractional tax rate snapshot (e.g. 0.11 for PPN 11%).</summary>
    public decimal TaxRate { get; private set; }

    public string? Description { get; private set; }
    public decimal LineSubTotal { get; private set; }
    public decimal LineTaxAmount { get; private set; }
    public decimal LineTotal { get; private set; }

    /// <summary>Cumulative quantity received via goods receipts (BR-PUR-2).</summary>
    public decimal ReceivedQuantity { get; private set; }

    /// <summary>Cumulative quantity billed via purchase invoices (BR-PUR-3).</summary>
    public decimal InvoicedQuantity { get; private set; }

    public decimal OutstandingQuantity => Quantity - ReceivedQuantity;

    /// <summary>Received but not yet invoiced — the quantity a purchase invoice may still bill.</summary>
    public decimal UninvoicedReceivedQuantity => ReceivedQuantity - InvoicedQuantity;

    public bool IsFullyReceived => ReceivedQuantity >= Quantity;

    internal void Receive(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Received quantity must be positive.");
        }

        if (quantity > OutstandingQuantity)
        {
            throw new InvalidOperationException("Received quantity exceeds the outstanding quantity.");
        }

        ReceivedQuantity += quantity;
    }

    internal void Invoice(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Invoiced quantity must be positive.");
        }

        if (quantity > UninvoicedReceivedQuantity)
        {
            throw new InvalidOperationException("Invoiced quantity exceeds the received, uninvoiced quantity.");
        }

        InvoicedQuantity += quantity;
    }

    internal static PurchaseOrderLine Create(Guid productId, decimal quantity, decimal unitPrice, decimal taxRate, string? description)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Line quantity must be positive.");
        }

        if (unitPrice < 0 || taxRate is < 0 or > 1)
        {
            throw new InvalidOperationException("Invalid unit price or tax rate.");
        }

        return new PurchaseOrderLine(productId, quantity, unitPrice, taxRate, description?.Trim());
    }
}
