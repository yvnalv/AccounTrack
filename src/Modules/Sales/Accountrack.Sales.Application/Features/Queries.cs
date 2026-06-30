using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.MasterData;
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
            l.Description, l.DeliveredQuantity, l.InvoicedQuantity, l.OutstandingQuantity))
            .ToList(),
        o.RowVersion);
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

public sealed record GetSalesInvoiceQuery(Guid Id) : IQuery<SalesInvoiceDto>;

public sealed class GetSalesInvoiceHandler : IQueryHandler<GetSalesInvoiceQuery, SalesInvoiceDto>
{
    private readonly ISalesInvoiceRepository _invoices;
    public GetSalesInvoiceHandler(ISalesInvoiceRepository invoices) => _invoices = invoices;

    public async Task<Result<SalesInvoiceDto>> Handle(GetSalesInvoiceQuery request, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(request.Id, ct);
        if (invoice is null)
        {
            return Error.NotFound("SALES.SI_NOT_FOUND", "Sales invoice not found.");
        }

        return new SalesInvoiceDto(
            invoice.Id, invoice.Number, invoice.SalesOrderId, invoice.CustomerId, invoice.Currency,
            invoice.InvoiceDate, invoice.DueDate, invoice.SubTotal, invoice.TaxTotal, invoice.GrandTotal,
            invoice.JournalEntryId, invoice.ArOpenItemId, invoice.Notes,
            invoice.Lines.Select(l => new SalesInvoiceLineDto(
                l.Id, l.SalesOrderLineId, l.ProductId, l.Quantity, l.UnitPrice, l.TaxRate, l.LineNet, l.LineTax, l.LineTotal,
                l.ReturnableQuantity))
                .ToList());
    }
}

public sealed record GetSalesInvoicesForSalesOrderQuery(Guid SalesOrderId)
    : IQuery<IReadOnlyList<SalesInvoiceSummaryDto>>;

public sealed class GetSalesInvoicesForSalesOrderHandler
    : IQueryHandler<GetSalesInvoicesForSalesOrderQuery, IReadOnlyList<SalesInvoiceSummaryDto>>
{
    private readonly ISalesInvoiceRepository _invoices;
    public GetSalesInvoicesForSalesOrderHandler(ISalesInvoiceRepository invoices) => _invoices = invoices;

    public async Task<Result<IReadOnlyList<SalesInvoiceSummaryDto>>> Handle(
        GetSalesInvoicesForSalesOrderQuery request, CancellationToken ct)
    {
        var invoices = await _invoices.ListBySalesOrderAsync(request.SalesOrderId, ct);
        return Result.Success<IReadOnlyList<SalesInvoiceSummaryDto>>(invoices
            .Select(i => new SalesInvoiceSummaryDto(
                i.Id, i.Number, i.SalesOrderId, i.InvoiceDate, i.DueDate, i.GrandTotal, i.JournalEntryId))
            .ToList());
    }
}

public sealed record GetCustomerPaymentQuery(Guid Id) : IQuery<CustomerPaymentDto>;

public sealed class GetCustomerPaymentHandler : IQueryHandler<GetCustomerPaymentQuery, CustomerPaymentDto>
{
    private readonly ICustomerPaymentRepository _payments;
    public GetCustomerPaymentHandler(ICustomerPaymentRepository payments) => _payments = payments;

    public async Task<Result<CustomerPaymentDto>> Handle(GetCustomerPaymentQuery request, CancellationToken ct)
    {
        var payment = await _payments.GetByIdAsync(request.Id, ct);
        if (payment is null)
        {
            return Error.NotFound("SALES.PAYMENT_NOT_FOUND", "Customer payment not found.");
        }

        return new CustomerPaymentDto(
            payment.Id, payment.Number, payment.CustomerId, payment.CashAccountId, payment.Currency,
            payment.PaymentDate, payment.TotalAmount, payment.JournalEntryId, payment.Reference, payment.Notes,
            payment.Allocations.Select(a => new CustomerPaymentAllocationDto(a.ArOpenItemId, a.Amount)).ToList());
    }
}

public sealed record GetCustomerPaymentsQuery(Guid CustomerId) : IQuery<IReadOnlyList<CustomerPaymentSummaryDto>>;

public sealed class GetCustomerPaymentsHandler : IQueryHandler<GetCustomerPaymentsQuery, IReadOnlyList<CustomerPaymentSummaryDto>>
{
    private readonly ICustomerPaymentRepository _payments;
    public GetCustomerPaymentsHandler(ICustomerPaymentRepository payments) => _payments = payments;

    public async Task<Result<IReadOnlyList<CustomerPaymentSummaryDto>>> Handle(GetCustomerPaymentsQuery request, CancellationToken ct)
    {
        var payments = await _payments.ListByCustomerAsync(request.CustomerId, ct);
        return Result.Success<IReadOnlyList<CustomerPaymentSummaryDto>>(payments
            .Select(p => new CustomerPaymentSummaryDto(p.Id, p.Number, p.CustomerId, p.PaymentDate, p.TotalAmount, p.JournalEntryId))
            .ToList());
    }
}

public sealed record GetSalesReturnQuery(Guid Id) : IQuery<SalesReturnDto>;

public sealed class GetSalesReturnHandler : IQueryHandler<GetSalesReturnQuery, SalesReturnDto>
{
    private readonly ISalesReturnRepository _returns;
    public GetSalesReturnHandler(ISalesReturnRepository returns) => _returns = returns;

    public async Task<Result<SalesReturnDto>> Handle(GetSalesReturnQuery request, CancellationToken ct)
    {
        var r = await _returns.GetByIdAsync(request.Id, ct);
        if (r is null)
        {
            return Error.NotFound("SALES.RETURN_NOT_FOUND", "Sales return not found.");
        }

        return new SalesReturnDto(
            r.Id, r.Number, r.SalesInvoiceId, r.SalesOrderId, r.CustomerId, r.WarehouseId, r.Currency,
            r.ReturnDate, r.SubTotal, r.TaxTotal, r.GrandTotal, r.TotalCost, r.JournalEntryId, r.Notes,
            r.Lines.Select(l => new SalesReturnLineDto(
                l.SalesInvoiceLineId, l.ProductId, l.Quantity, l.UnitPrice, l.TaxRate, l.UnitCost,
                l.LineNet, l.LineTax, l.LineTotal, l.LineCost)).ToList());
    }
}

public sealed record GetReturnsForSalesOrderQuery(Guid SalesOrderId) : IQuery<IReadOnlyList<SalesReturnSummaryDto>>;

public sealed class GetReturnsForSalesOrderHandler
    : IQueryHandler<GetReturnsForSalesOrderQuery, IReadOnlyList<SalesReturnSummaryDto>>
{
    private readonly ISalesReturnRepository _returns;
    public GetReturnsForSalesOrderHandler(ISalesReturnRepository returns) => _returns = returns;

    public async Task<Result<IReadOnlyList<SalesReturnSummaryDto>>> Handle(
        GetReturnsForSalesOrderQuery request, CancellationToken ct)
    {
        var items = await _returns.ListBySalesOrderAsync(request.SalesOrderId, ct);
        return Result.Success<IReadOnlyList<SalesReturnSummaryDto>>(items
            .Select(r => new SalesReturnSummaryDto(r.Id, r.Number, r.SalesInvoiceId, r.ReturnDate, r.GrandTotal, r.JournalEntryId))
            .ToList());
    }
}

public sealed record GetSalesReturnsQuery : IQuery<IReadOnlyList<SalesReturnListItemDto>>;

public sealed class GetSalesReturnsHandler : IQueryHandler<GetSalesReturnsQuery, IReadOnlyList<SalesReturnListItemDto>>
{
    private readonly ISalesReturnRepository _returns;
    private readonly IMasterDataLookup _masterData;
    public GetSalesReturnsHandler(ISalesReturnRepository returns, IMasterDataLookup masterData)
    {
        _returns = returns;
        _masterData = masterData;
    }

    public async Task<Result<IReadOnlyList<SalesReturnListItemDto>>> Handle(
        GetSalesReturnsQuery request, CancellationToken ct)
    {
        var items = await _returns.ListAsync(ct);
        var names = await _masterData.ResolveNamesAsync(items.Select(r => r.CustomerId).Distinct().ToList(), ct);
        return Result.Success<IReadOnlyList<SalesReturnListItemDto>>(items
            .Select(r => new SalesReturnListItemDto(
                r.Id, r.Number, r.ReturnDate, r.CustomerId, names.GetValueOrDefault(r.CustomerId, "—"),
                r.GrandTotal, r.JournalEntryId))
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
