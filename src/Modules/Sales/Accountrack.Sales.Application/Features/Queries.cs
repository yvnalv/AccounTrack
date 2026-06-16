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
            l.Description, l.DeliveredQuantity))
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
