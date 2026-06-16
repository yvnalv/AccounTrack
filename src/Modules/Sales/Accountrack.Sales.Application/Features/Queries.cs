using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Application.Contracts;
using Accountrack.Sales.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Sales.Application.Features;

internal static class SalesOrderMapping
{
    public static SalesOrderDto ToDto(this SalesOrder o) => new(
        o.Id, o.Number, o.CustomerId, o.WarehouseId, o.Currency, o.OrderDate, o.Status.ToString(),
        o.ApprovalRequestId, o.SubTotal, o.TaxTotal, o.GrandTotal, o.Notes,
        o.Lines.Select(l => new SalesOrderLineDto(
            l.Id, l.ProductId, l.Quantity, l.UnitPrice, l.TaxRate, l.LineSubTotal, l.LineTaxAmount, l.LineTotal,
            l.Description, l.DeliveredQuantity, l.OutstandingQuantity))
            .ToList());
}

public sealed record GetSalesOrderQuery(Guid Id) : IQuery<SalesOrderDto>;

public sealed class GetSalesOrderHandler : IQueryHandler<GetSalesOrderQuery, SalesOrderDto>
{
    private readonly ISalesOrderRepository _orders;
    public GetSalesOrderHandler(ISalesOrderRepository orders) => _orders = orders;

    public async Task<Result<SalesOrderDto>> Handle(GetSalesOrderQuery request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(request.Id, ct);
        return order is null ? SalesErrors.NotFound : order.ToDto();
    }
}

public sealed record GetDeliveryOrderQuery(Guid Id) : IQuery<DeliveryOrderDto>;

public sealed class GetDeliveryOrderHandler : IQueryHandler<GetDeliveryOrderQuery, DeliveryOrderDto>
{
    private readonly IDeliveryOrderRepository _deliveries;
    public GetDeliveryOrderHandler(IDeliveryOrderRepository deliveries) => _deliveries = deliveries;

    public async Task<Result<DeliveryOrderDto>> Handle(GetDeliveryOrderQuery request, CancellationToken ct)
    {
        var delivery = await _deliveries.GetByIdAsync(request.Id, ct);
        if (delivery is null)
        {
            return Error.NotFound("SALES.DO_NOT_FOUND", "Delivery order not found.");
        }

        return new DeliveryOrderDto(
            delivery.Id, delivery.Number, delivery.SalesOrderId, delivery.CustomerId, delivery.WarehouseId,
            delivery.Currency, delivery.DeliveryDate, delivery.TotalCost, delivery.JournalEntryId, delivery.Notes,
            delivery.Lines.Select(l => new DeliveryOrderLineDto(
                l.SalesOrderLineId, l.ProductId, l.Quantity, l.UnitCost, l.LineCost)).ToList());
    }
}

public sealed record GetDeliveriesForSalesOrderQuery(Guid SalesOrderId)
    : IQuery<IReadOnlyList<DeliveryOrderSummaryDto>>;

public sealed class GetDeliveriesForSalesOrderHandler
    : IQueryHandler<GetDeliveriesForSalesOrderQuery, IReadOnlyList<DeliveryOrderSummaryDto>>
{
    private readonly IDeliveryOrderRepository _deliveries;
    public GetDeliveriesForSalesOrderHandler(IDeliveryOrderRepository deliveries) => _deliveries = deliveries;

    public async Task<Result<IReadOnlyList<DeliveryOrderSummaryDto>>> Handle(
        GetDeliveriesForSalesOrderQuery request, CancellationToken ct)
    {
        var deliveries = await _deliveries.ListBySalesOrderAsync(request.SalesOrderId, ct);
        return Result.Success<IReadOnlyList<DeliveryOrderSummaryDto>>(deliveries
            .Select(d => new DeliveryOrderSummaryDto(d.Id, d.Number, d.SalesOrderId, d.DeliveryDate, d.TotalCost, d.JournalEntryId))
            .ToList());
    }
}

public sealed record GetSalesOrdersQuery : IQuery<IReadOnlyList<SalesOrderSummaryDto>>;

public sealed class GetSalesOrdersHandler : IQueryHandler<GetSalesOrdersQuery, IReadOnlyList<SalesOrderSummaryDto>>
{
    private readonly ISalesOrderRepository _orders;
    public GetSalesOrdersHandler(ISalesOrderRepository orders) => _orders = orders;

    public async Task<Result<IReadOnlyList<SalesOrderSummaryDto>>> Handle(GetSalesOrdersQuery request, CancellationToken ct)
    {
        var orders = await _orders.ListAsync(ct);
        return Result.Success<IReadOnlyList<SalesOrderSummaryDto>>(orders
            .Select(o => new SalesOrderSummaryDto(o.Id, o.Number, o.CustomerId, o.Status.ToString(), o.GrandTotal, o.OrderDate))
            .ToList());
    }
}
