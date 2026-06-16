using Accountrack.SharedKernel.Domain;

namespace Accountrack.Sales.Domain;

/// <summary>
/// A delivery order: goods shipped against a sales order (order-to-cash — slice 2). Posting a
/// delivery issues stock (moving-average) and posts Dr COGS / Cr Inventory at the issue cost,
/// atomically (docs/POSTING_RULES.md, INTEGRATION_EVENTS.md §2). Immutable once posted.
/// </summary>
public sealed class DeliveryOrder : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<DeliveryOrderLine> _lines = new();

    private DeliveryOrder() { }

    private DeliveryOrder(
        string number, Guid salesOrderId, Guid customerId, Guid warehouseId, string currency,
        DateOnly deliveryDate, string? notes)
    {
        Number = number;
        SalesOrderId = salesOrderId;
        CustomerId = customerId;
        WarehouseId = warehouseId;
        Currency = currency;
        DeliveryDate = deliveryDate;
        Notes = notes;
    }

    public string Number { get; private set; } = default!;
    public Guid SalesOrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateOnly DeliveryDate { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>The GL journal posted for this delivery (Dr COGS / Cr Inventory).</summary>
    public Guid? JournalEntryId { get; private set; }

    public IReadOnlyList<DeliveryOrderLine> Lines => _lines;

    /// <summary>Total cost of goods shipped (the COGS amount).</summary>
    public decimal TotalCost => _lines.Sum(l => l.LineCost);

    public static DeliveryOrder Create(
        string number, Guid salesOrderId, Guid customerId, Guid warehouseId, string currency,
        DateOnly deliveryDate, string? notes) =>
        new(number, salesOrderId, customerId, warehouseId, currency, deliveryDate, notes?.Trim());

    public void AddLine(Guid salesOrderLineId, Guid productId, decimal quantity, decimal unitCost)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Delivery line quantity must be positive.");
        }

        _lines.Add(DeliveryOrderLine.Create(salesOrderLineId, productId, quantity, unitCost));
    }

    public void SetJournal(Guid journalEntryId) => JournalEntryId = journalEntryId;
}

/// <summary>A delivery-order line: quantity shipped at the moving-average issue cost.</summary>
public sealed class DeliveryOrderLine : Entity
{
    private DeliveryOrderLine() { }

    private DeliveryOrderLine(Guid salesOrderLineId, Guid productId, decimal quantity, decimal unitCost)
    {
        SalesOrderLineId = salesOrderLineId;
        ProductId = productId;
        Quantity = quantity;
        UnitCost = unitCost;
        LineCost = Math.Round(quantity * unitCost, 4, MidpointRounding.ToEven);
    }

    public Guid DeliveryOrderId { get; private set; }
    public Guid SalesOrderLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal LineCost { get; private set; }

    internal static DeliveryOrderLine Create(Guid salesOrderLineId, Guid productId, decimal quantity, decimal unitCost) =>
        new(salesOrderLineId, productId, quantity, unitCost);
}
