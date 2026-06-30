using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.MasterData;
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
            l.Id, l.ProductId, l.Quantity, l.UnitPrice, l.TaxRate, l.LineSubTotal, l.LineTaxAmount, l.LineTotal,
            l.Description, l.ReceivedQuantity, l.InvoicedQuantity, l.OutstandingQuantity))
            .ToList(),
        o.RowVersion);
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

public sealed record GetGoodsReceiptQuery(Guid Id) : IQuery<GoodsReceiptDto>;

public sealed class GetGoodsReceiptHandler : IQueryHandler<GetGoodsReceiptQuery, GoodsReceiptDto>
{
    private readonly IGoodsReceiptRepository _receipts;
    public GetGoodsReceiptHandler(IGoodsReceiptRepository receipts) => _receipts = receipts;

    public async Task<Result<GoodsReceiptDto>> Handle(GetGoodsReceiptQuery request, CancellationToken ct)
    {
        var receipt = await _receipts.GetByIdAsync(request.Id, ct);
        if (receipt is null)
        {
            return Error.NotFound("PURCHASING.GR_NOT_FOUND", "Goods receipt not found.");
        }

        return new GoodsReceiptDto(
            receipt.Id, receipt.Number, receipt.PurchaseOrderId, receipt.SupplierId, receipt.WarehouseId,
            receipt.Currency, receipt.ReceiptDate, receipt.TotalCost, receipt.JournalEntryId, receipt.Notes,
            receipt.Lines.Select(l => new GoodsReceiptLineDto(
                l.PurchaseOrderLineId, l.ProductId, l.Quantity, l.UnitCost, l.LineCost)).ToList());
    }
}

public sealed record GetGoodsReceiptsForPurchaseOrderQuery(Guid PurchaseOrderId)
    : IQuery<IReadOnlyList<GoodsReceiptSummaryDto>>;

public sealed class GetGoodsReceiptsForPurchaseOrderHandler
    : IQueryHandler<GetGoodsReceiptsForPurchaseOrderQuery, IReadOnlyList<GoodsReceiptSummaryDto>>
{
    private readonly IGoodsReceiptRepository _receipts;
    public GetGoodsReceiptsForPurchaseOrderHandler(IGoodsReceiptRepository receipts) => _receipts = receipts;

    public async Task<Result<IReadOnlyList<GoodsReceiptSummaryDto>>> Handle(
        GetGoodsReceiptsForPurchaseOrderQuery request, CancellationToken ct)
    {
        var receipts = await _receipts.ListByPurchaseOrderAsync(request.PurchaseOrderId, ct);
        return Result.Success<IReadOnlyList<GoodsReceiptSummaryDto>>(receipts
            .Select(r => new GoodsReceiptSummaryDto(r.Id, r.Number, r.PurchaseOrderId, r.ReceiptDate, r.TotalCost, r.JournalEntryId))
            .ToList());
    }
}

public sealed record GetPurchaseInvoiceQuery(Guid Id) : IQuery<PurchaseInvoiceDto>;

public sealed class GetPurchaseInvoiceHandler : IQueryHandler<GetPurchaseInvoiceQuery, PurchaseInvoiceDto>
{
    private readonly IPurchaseInvoiceRepository _invoices;
    public GetPurchaseInvoiceHandler(IPurchaseInvoiceRepository invoices) => _invoices = invoices;

    public async Task<Result<PurchaseInvoiceDto>> Handle(GetPurchaseInvoiceQuery request, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(request.Id, ct);
        if (invoice is null)
        {
            return Error.NotFound("PURCHASING.PI_NOT_FOUND", "Purchase invoice not found.");
        }

        return new PurchaseInvoiceDto(
            invoice.Id, invoice.Number, invoice.SupplierInvoiceNo, invoice.PurchaseOrderId, invoice.SupplierId,
            invoice.Currency, invoice.InvoiceDate, invoice.DueDate, invoice.SubTotal, invoice.TaxTotal,
            invoice.GrandTotal, invoice.JournalEntryId, invoice.ApOpenItemId, invoice.Notes,
            invoice.Lines.Select(l => new PurchaseInvoiceLineDto(
                l.Id, l.PurchaseOrderLineId, l.ProductId, l.Quantity, l.UnitPrice, l.TaxRate, l.LineNet, l.LineTax, l.LineTotal,
                l.ReturnableQuantity))
                .ToList());
    }
}

public sealed record GetPurchaseInvoicesForPurchaseOrderQuery(Guid PurchaseOrderId)
    : IQuery<IReadOnlyList<PurchaseInvoiceSummaryDto>>;

public sealed class GetPurchaseInvoicesForPurchaseOrderHandler
    : IQueryHandler<GetPurchaseInvoicesForPurchaseOrderQuery, IReadOnlyList<PurchaseInvoiceSummaryDto>>
{
    private readonly IPurchaseInvoiceRepository _invoices;
    public GetPurchaseInvoicesForPurchaseOrderHandler(IPurchaseInvoiceRepository invoices) => _invoices = invoices;

    public async Task<Result<IReadOnlyList<PurchaseInvoiceSummaryDto>>> Handle(
        GetPurchaseInvoicesForPurchaseOrderQuery request, CancellationToken ct)
    {
        var invoices = await _invoices.ListByPurchaseOrderAsync(request.PurchaseOrderId, ct);
        return Result.Success<IReadOnlyList<PurchaseInvoiceSummaryDto>>(invoices
            .Select(i => new PurchaseInvoiceSummaryDto(
                i.Id, i.Number, i.PurchaseOrderId, i.InvoiceDate, i.DueDate, i.GrandTotal, i.JournalEntryId))
            .ToList());
    }
}

public sealed record GetSupplierPaymentQuery(Guid Id) : IQuery<SupplierPaymentDto>;

public sealed class GetSupplierPaymentHandler : IQueryHandler<GetSupplierPaymentQuery, SupplierPaymentDto>
{
    private readonly ISupplierPaymentRepository _payments;
    public GetSupplierPaymentHandler(ISupplierPaymentRepository payments) => _payments = payments;

    public async Task<Result<SupplierPaymentDto>> Handle(GetSupplierPaymentQuery request, CancellationToken ct)
    {
        var payment = await _payments.GetByIdAsync(request.Id, ct);
        if (payment is null)
        {
            return Error.NotFound("PURCHASING.PAYMENT_NOT_FOUND", "Supplier payment not found.");
        }

        return new SupplierPaymentDto(
            payment.Id, payment.Number, payment.SupplierId, payment.CashAccountId, payment.Currency,
            payment.PaymentDate, payment.TotalAmount, payment.JournalEntryId, payment.Reference, payment.Notes,
            payment.Allocations.Select(a => new SupplierPaymentAllocationDto(a.ApOpenItemId, a.Amount)).ToList());
    }
}

public sealed record GetSupplierPaymentsQuery(Guid SupplierId) : IQuery<IReadOnlyList<SupplierPaymentSummaryDto>>;

public sealed class GetSupplierPaymentsHandler : IQueryHandler<GetSupplierPaymentsQuery, IReadOnlyList<SupplierPaymentSummaryDto>>
{
    private readonly ISupplierPaymentRepository _payments;
    public GetSupplierPaymentsHandler(ISupplierPaymentRepository payments) => _payments = payments;

    public async Task<Result<IReadOnlyList<SupplierPaymentSummaryDto>>> Handle(GetSupplierPaymentsQuery request, CancellationToken ct)
    {
        var payments = await _payments.ListBySupplierAsync(request.SupplierId, ct);
        return Result.Success<IReadOnlyList<SupplierPaymentSummaryDto>>(payments
            .Select(p => new SupplierPaymentSummaryDto(p.Id, p.Number, p.SupplierId, p.PaymentDate, p.TotalAmount, p.JournalEntryId))
            .ToList());
    }
}

public sealed record GetPurchaseReturnQuery(Guid Id) : IQuery<PurchaseReturnDto>;

public sealed class GetPurchaseReturnHandler : IQueryHandler<GetPurchaseReturnQuery, PurchaseReturnDto>
{
    private readonly IPurchaseReturnRepository _returns;
    public GetPurchaseReturnHandler(IPurchaseReturnRepository returns) => _returns = returns;

    public async Task<Result<PurchaseReturnDto>> Handle(GetPurchaseReturnQuery request, CancellationToken ct)
    {
        var r = await _returns.GetByIdAsync(request.Id, ct);
        if (r is null)
        {
            return Error.NotFound("PURCHASING.RETURN_NOT_FOUND", "Purchase return not found.");
        }

        return new PurchaseReturnDto(
            r.Id, r.Number, r.PurchaseInvoiceId, r.PurchaseOrderId, r.SupplierId, r.WarehouseId, r.Currency,
            r.ReturnDate, r.SubTotal, r.TaxTotal, r.GrandTotal, r.TotalCost, r.JournalEntryId, r.Notes,
            r.Lines.Select(l => new PurchaseReturnLineDto(
                l.PurchaseInvoiceLineId, l.ProductId, l.Quantity, l.UnitPrice, l.TaxRate, l.UnitCost,
                l.LineNet, l.LineTax, l.LineTotal, l.LineCost)).ToList());
    }
}

public sealed record GetReturnsForPurchaseOrderQuery(Guid PurchaseOrderId) : IQuery<IReadOnlyList<PurchaseReturnSummaryDto>>;

public sealed class GetReturnsForPurchaseOrderHandler
    : IQueryHandler<GetReturnsForPurchaseOrderQuery, IReadOnlyList<PurchaseReturnSummaryDto>>
{
    private readonly IPurchaseReturnRepository _returns;
    public GetReturnsForPurchaseOrderHandler(IPurchaseReturnRepository returns) => _returns = returns;

    public async Task<Result<IReadOnlyList<PurchaseReturnSummaryDto>>> Handle(
        GetReturnsForPurchaseOrderQuery request, CancellationToken ct)
    {
        var items = await _returns.ListByPurchaseOrderAsync(request.PurchaseOrderId, ct);
        return Result.Success<IReadOnlyList<PurchaseReturnSummaryDto>>(items
            .Select(r => new PurchaseReturnSummaryDto(r.Id, r.Number, r.PurchaseInvoiceId, r.ReturnDate, r.GrandTotal, r.JournalEntryId))
            .ToList());
    }
}

public sealed record GetPurchaseReturnsQuery : IQuery<IReadOnlyList<PurchaseReturnListItemDto>>;

public sealed class GetPurchaseReturnsHandler : IQueryHandler<GetPurchaseReturnsQuery, IReadOnlyList<PurchaseReturnListItemDto>>
{
    private readonly IPurchaseReturnRepository _returns;
    private readonly IMasterDataLookup _masterData;
    public GetPurchaseReturnsHandler(IPurchaseReturnRepository returns, IMasterDataLookup masterData)
    {
        _returns = returns;
        _masterData = masterData;
    }

    public async Task<Result<IReadOnlyList<PurchaseReturnListItemDto>>> Handle(
        GetPurchaseReturnsQuery request, CancellationToken ct)
    {
        var items = await _returns.ListAsync(ct);
        var names = await _masterData.ResolveNamesAsync(items.Select(r => r.SupplierId).Distinct().ToList(), ct);
        return Result.Success<IReadOnlyList<PurchaseReturnListItemDto>>(items
            .Select(r => new PurchaseReturnListItemDto(
                r.Id, r.Number, r.ReturnDate, r.SupplierId, names.GetValueOrDefault(r.SupplierId, "—"),
                r.GrandTotal, r.JournalEntryId))
            .ToList());
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
