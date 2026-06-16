using Accountrack.SharedKernel.Domain;

namespace Accountrack.Sales.Domain;

public enum SalesOrderStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    PartiallyDelivered = 5,
    Delivered = 6,
}

/// <summary>
/// A sales order (order-to-cash — slice 1). Created as a draft, then submitted for approval; its
/// status is advanced by the Approval Workflow via integration events. Delivery (stock issue + COGS),
/// invoicing (AR/Revenue/VAT) and customer payment are later slices.
/// </summary>
public sealed class SalesOrder : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<SalesOrderLine> _lines = new();

    private SalesOrder() { }

    private SalesOrder(string number, Guid customerId, Guid warehouseId, string currency, DateOnly orderDate, string? notes)
    {
        Number = number;
        CustomerId = customerId;
        WarehouseId = warehouseId;
        Currency = currency;
        OrderDate = orderDate;
        Notes = notes;
        Status = SalesOrderStatus.Draft;
    }

    public string Number { get; private set; } = default!;
    public Guid CustomerId { get; private set; }

    /// <summary>The warehouse goods will be shipped from.</summary>
    public Guid WarehouseId { get; private set; }

    public string Currency { get; private set; } = default!;
    public DateOnly OrderDate { get; private set; }
    public string? Notes { get; private set; }
    public SalesOrderStatus Status { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }

    public decimal SubTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal GrandTotal { get; private set; }

    public IReadOnlyList<SalesOrderLine> Lines => _lines;

    public static SalesOrder CreateDraft(
        string number, Guid customerId, Guid warehouseId, string currency, DateOnly orderDate, string? notes) =>
        new(number, customerId, warehouseId, currency.Trim().ToUpperInvariant(), orderDate, notes?.Trim());

    public void AddLine(Guid productId, decimal quantity, decimal unitPrice, decimal taxRate, string? description)
    {
        if (Status != SalesOrderStatus.Draft)
        {
            throw new InvalidOperationException("Lines can only be changed on a draft sales order.");
        }

        _lines.Add(SalesOrderLine.Create(productId, quantity, unitPrice, taxRate, description));
        Recalculate();
    }

    public void MarkPendingApproval(Guid approvalRequestId)
    {
        EnsureDraftWithLines();
        ApprovalRequestId = approvalRequestId;
        Status = SalesOrderStatus.PendingApproval;
    }

    public void MarkAutoApproved(Guid approvalRequestId)
    {
        EnsureDraftWithLines();
        ApprovalRequestId = approvalRequestId;
        Status = SalesOrderStatus.Approved;
    }

    public void MarkApproved()
    {
        if (Status == SalesOrderStatus.PendingApproval)
        {
            Status = SalesOrderStatus.Approved;
        }
    }

    public void MarkRejected()
    {
        if (Status == SalesOrderStatus.PendingApproval)
        {
            Status = SalesOrderStatus.Rejected;
        }
    }

    public void Cancel()
    {
        if (Status is SalesOrderStatus.Approved or SalesOrderStatus.Rejected)
        {
            throw new InvalidOperationException("A decided sales order cannot be cancelled.");
        }

        Status = SalesOrderStatus.Cancelled;
    }

    /// <summary>Whether goods can still be shipped against this order.</summary>
    public bool CanDeliver =>
        Status is SalesOrderStatus.Approved or SalesOrderStatus.PartiallyDelivered;

    /// <summary>
    /// Records a shipped quantity against one line and advances the order's delivery status
    /// (BR-SAL-2). Throws if the order is not deliverable or the quantity exceeds what's outstanding.
    /// </summary>
    public void DeliverLine(Guid lineId, decimal quantity)
    {
        if (!CanDeliver)
        {
            throw new InvalidOperationException("Goods can only be delivered against an approved sales order.");
        }

        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException("Sales-order line not found.");

        line.Deliver(quantity);

        Status = _lines.All(l => l.IsFullyDelivered)
            ? SalesOrderStatus.Delivered
            : SalesOrderStatus.PartiallyDelivered;
    }

    private void EnsureDraftWithLines()
    {
        if (Status != SalesOrderStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft sales order can be submitted.");
        }

        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("A sales order requires at least one line.");
        }
    }

    private void Recalculate()
    {
        SubTotal = Math.Round(_lines.Sum(l => l.LineSubTotal), 4, MidpointRounding.ToEven);
        TaxTotal = Math.Round(_lines.Sum(l => l.LineTaxAmount), 4, MidpointRounding.ToEven);
        GrandTotal = SubTotal + TaxTotal;
    }
}

/// <summary>A sales-order line: quantity × unit price, plus a snapshotted tax rate.</summary>
public sealed class SalesOrderLine : Entity
{
    private SalesOrderLine() { }

    private SalesOrderLine(Guid productId, decimal quantity, decimal unitPrice, decimal taxRate, string? description)
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

    public Guid SalesOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    /// <summary>Fractional tax rate snapshot (e.g. 0.11 for PPN 11%).</summary>
    public decimal TaxRate { get; private set; }

    public string? Description { get; private set; }
    public decimal LineSubTotal { get; private set; }
    public decimal LineTaxAmount { get; private set; }
    public decimal LineTotal { get; private set; }

    /// <summary>Cumulative quantity shipped via delivery orders (BR-SAL-2).</summary>
    public decimal DeliveredQuantity { get; private set; }

    public decimal OutstandingQuantity => Quantity - DeliveredQuantity;

    public bool IsFullyDelivered => DeliveredQuantity >= Quantity;

    internal void Deliver(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Delivered quantity must be positive.");
        }

        if (quantity > OutstandingQuantity)
        {
            throw new InvalidOperationException("Delivered quantity exceeds the outstanding quantity.");
        }

        DeliveredQuantity += quantity;
    }

    internal static SalesOrderLine Create(Guid productId, decimal quantity, decimal unitPrice, decimal taxRate, string? description)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Line quantity must be positive.");
        }

        if (unitPrice < 0 || taxRate is < 0 or > 1)
        {
            throw new InvalidOperationException("Invalid unit price or tax rate.");
        }

        return new SalesOrderLine(productId, quantity, unitPrice, taxRate, description?.Trim());
    }
}
