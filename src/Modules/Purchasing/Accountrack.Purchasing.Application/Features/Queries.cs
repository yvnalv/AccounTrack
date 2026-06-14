using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Application.Contracts;
using Accountrack.Purchasing.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Purchasing.Application.Features;

internal static class PurchaseOrderMapping
{
    public static PurchaseOrderDto ToDto(this PurchaseOrder o) => new(
        o.Id, o.Number, o.SupplierId, o.WarehouseId, o.Currency, o.OrderDate, o.Status.ToString(),
        o.ApprovalRequestId, o.SubTotal, o.TaxTotal, o.GrandTotal, o.Notes,
        o.Lines.Select(l => new PurchaseOrderLineDto(
            l.ProductId, l.Quantity, l.UnitPrice, l.TaxRate, l.LineSubTotal, l.LineTaxAmount, l.LineTotal, l.Description))
            .ToList());
}

public sealed record GetPurchaseOrderQuery(Guid Id) : IQuery<PurchaseOrderDto>;

public sealed class GetPurchaseOrderHandler : IQueryHandler<GetPurchaseOrderQuery, PurchaseOrderDto>
{
    private readonly IPurchaseOrderRepository _orders;
    public GetPurchaseOrderHandler(IPurchaseOrderRepository orders) => _orders = orders;

    public async Task<Result<PurchaseOrderDto>> Handle(GetPurchaseOrderQuery request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(request.Id, ct);
        return order is null ? PurchasingErrors.NotFound : order.ToDto();
    }
}

public sealed record GetPurchaseOrdersQuery : IQuery<IReadOnlyList<PurchaseOrderSummaryDto>>;

public sealed class GetPurchaseOrdersHandler : IQueryHandler<GetPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderSummaryDto>>
{
    private readonly IPurchaseOrderRepository _orders;
    public GetPurchaseOrdersHandler(IPurchaseOrderRepository orders) => _orders = orders;

    public async Task<Result<IReadOnlyList<PurchaseOrderSummaryDto>>> Handle(GetPurchaseOrdersQuery request, CancellationToken ct)
    {
        var orders = await _orders.ListAsync(ct);
        return Result.Success<IReadOnlyList<PurchaseOrderSummaryDto>>(orders
            .Select(o => new PurchaseOrderSummaryDto(o.Id, o.Number, o.SupplierId, o.Status.ToString(), o.GrandTotal, o.OrderDate))
            .ToList());
    }
}
