using Accountrack.Sales.Application.Features;
using Accountrack.SharedKernel.Results;
using Accountrack.Web.Common.Export;
using Accountrack.Web.Common.Pdf;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Sales.Api;

public static class SalesEndpoints
{
    public static IEndpointRouteBuilder MapSalesEndpoints(this IEndpointRouteBuilder app)
    {
        var so = app.MapGroup("/api/v1/sales-orders").WithTags("Sales").RequireAuthorization();

        so.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetSalesOrdersQuery(), ct)))
            .RequireAuthorization("Sales.View").WithName("GetSalesOrders");

        so.MapGet("/export", (string? format, ISender s, CancellationToken ct) =>
                TableExport.File(s.Send(new ExportSalesOrdersQuery(), ct), "sales-orders", format))
            .RequireAuthorization("Sales.View").WithName("ExportSalesOrders");

        so.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => Send(s.Send(new GetSalesOrderQuery(id), ct)))
            .RequireAuthorization("Sales.View").WithName("GetSalesOrder");

        so.MapGet("/{id:guid}/quotation-pdf", (Guid id, ISender s, CancellationToken ct) =>
                PdfRenderer.File(s.Send(new GetQuotationPdfQuery(id), ct), "quotation"))
            .RequireAuthorization("Sales.View").WithName("GetQuotationPdf");

        so.MapPost("/", (CreateSalesOrderCommand c, ISender s, CancellationToken ct) =>
                Created(s.Send(c, ct), "/api/v1/sales-orders"))
            .RequireAuthorization("Sales.Create").WithName("CreateSalesOrder");

        so.MapPost("/{id:guid}/submit", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new SubmitSalesOrderCommand(id), ct)))
            .RequireAuthorization("Sales.Create").WithName("SubmitSalesOrder");

        so.MapPut("/{id:guid}", (Guid id, UpdateSalesOrderBody body, ISender s, CancellationToken ct) =>
                Send(s.Send(new UpdateSalesOrderCommand(
                    id, body.CustomerId, body.WarehouseId, body.OrderDate, body.Notes, body.Lines, body.RowVersion), ct)))
            .RequireAuthorization("Sales.Edit").WithName("UpdateSalesOrder");

        so.MapPost("/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new CancelSalesOrderCommand(id), ct)))
            .RequireAuthorization("Sales.Cancel").WithName("CancelSalesOrder");

        // --- Delivery orders (against a sales order) ---
        so.MapGet("/{id:guid}/deliveries", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetDeliveriesForSalesOrderQuery(id), ct)))
            .RequireAuthorization("Sales.View").WithName("GetDeliveriesForSalesOrder");

        so.MapPost("/{id:guid}/deliveries", (Guid id, ShipGoodsRequest body, ISender s, CancellationToken ct) =>
                Created(s.Send(new PostDeliveryOrderCommand(id, body.DeliveryDate, body.Notes, body.Lines), ct),
                    $"/api/v1/sales-orders/{id}/deliveries"))
            .RequireAuthorization("Sales.Post").WithName("PostDeliveryOrder");

        var del = app.MapGroup("/api/v1/delivery-orders").WithTags("Sales").RequireAuthorization();

        del.MapGet("/", (ISender s, CancellationToken ct) =>
                Send(s.Send(new GetDeliveriesQuery(), ct)))
            .RequireAuthorization("Sales.View").WithName("GetDeliveries");

        del.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetDeliveryOrderQuery(id), ct)))
            .RequireAuthorization("Sales.View").WithName("GetDeliveryOrder");

        // --- Sales invoices (against a sales order) ---
        so.MapGet("/{id:guid}/invoices", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetSalesInvoicesForSalesOrderQuery(id), ct)))
            .RequireAuthorization("Sales.View").WithName("GetSalesInvoicesForSalesOrder");

        so.MapPost("/{id:guid}/invoices", (Guid id, BillCustomerRequest body, ISender s, CancellationToken ct) =>
                Created(s.Send(new PostSalesInvoiceCommand(id, body.InvoiceDate, body.DueDate, body.Notes, body.Lines), ct),
                    $"/api/v1/sales-orders/{id}/invoices"))
            .RequireAuthorization("Sales.Post").WithName("PostSalesInvoice");

        var si = app.MapGroup("/api/v1/sales-invoices").WithTags("Sales").RequireAuthorization();

        si.MapGet("/", (ISender s, CancellationToken ct) =>
                Send(s.Send(new GetSalesInvoicesQuery(), ct)))
            .RequireAuthorization("Sales.View").WithName("GetSalesInvoices");

        si.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetSalesInvoiceQuery(id), ct)))
            .RequireAuthorization("Sales.View").WithName("GetSalesInvoice");

        si.MapGet("/{id:guid}/pdf", (Guid id, ISender s, CancellationToken ct) =>
                PdfRenderer.File(s.Send(new GetSalesInvoicePdfQuery(id), ct), "invoice"))
            .RequireAuthorization("Sales.View").WithName("GetSalesInvoicePdf");

        si.MapPost("/{id:guid}/returns", (Guid id, ReturnGoodsRequest body, ISender s, CancellationToken ct) =>
                Created(s.Send(new PostSalesReturnCommand(id, body.ReturnDate, body.Notes, body.Lines, body.RefundCashAccountId), ct),
                    "/api/v1/sales-returns"))
            .RequireAuthorization("Sales.Post").WithName("PostSalesReturn");

        // --- Sales returns (credit notes) ---
        so.MapGet("/{id:guid}/returns", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetReturnsForSalesOrderQuery(id), ct)))
            .RequireAuthorization("Sales.View").WithName("GetReturnsForSalesOrder");

        var ret = app.MapGroup("/api/v1/sales-returns").WithTags("Sales").RequireAuthorization();

        ret.MapGet("/", (ISender s, CancellationToken ct) =>
                Send(s.Send(new GetSalesReturnsQuery(), ct)))
            .RequireAuthorization("Sales.View").WithName("GetSalesReturns");

        ret.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetSalesReturnQuery(id), ct)))
            .RequireAuthorization("Sales.View").WithName("GetSalesReturn");

        // --- Customer payments (receipts) ---
        var pay = app.MapGroup("/api/v1/customer-payments").WithTags("Sales").RequireAuthorization();

        pay.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetCustomerPaymentQuery(id), ct)))
            .RequireAuthorization("Sales.View").WithName("GetCustomerPayment");

        pay.MapGet("/", (Guid? customerId, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetCustomerPaymentsQuery(customerId), ct)))
            .RequireAuthorization("Sales.View").WithName("GetCustomerPayments");

        pay.MapPost("/", (PostCustomerPaymentCommand c, ISender s, CancellationToken ct) =>
                Created(s.Send(c, ct), "/api/v1/customer-payments"))
            .RequireAuthorization("Sales.Post").WithName("PostCustomerPayment");

        return app;
    }

    public sealed record UpdateSalesOrderBody(
        Guid CustomerId, Guid WarehouseId, DateOnly OrderDate, string? Notes, IReadOnlyList<CreateSoLine> Lines,
        byte[]? RowVersion);

    public sealed record ShipGoodsRequest(
        DateOnly DeliveryDate, string? Notes, IReadOnlyList<DeliveryOrderLineInput> Lines);

    public sealed record BillCustomerRequest(
        DateOnly InvoiceDate, DateOnly DueDate, string? Notes, IReadOnlyList<SalesInvoiceLineInput> Lines);

    public sealed record ReturnGoodsRequest(
        DateOnly ReturnDate, string? Notes, IReadOnlyList<SalesReturnLineInput> Lines, Guid? RefundCashAccountId = null);

    private static async Task<IResult> Send<T>(Task<Result<T>> task) => (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}
