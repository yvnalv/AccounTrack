using Accountrack.Purchasing.Application.Features;
using Accountrack.SharedKernel.Results;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Purchasing.Api;

public static class PurchasingEndpoints
{
    public static IEndpointRouteBuilder MapPurchasingEndpoints(this IEndpointRouteBuilder app)
    {
        var po = app.MapGroup("/api/v1/purchase-orders").WithTags("Purchasing").RequireAuthorization();

        po.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetPurchaseOrdersQuery(), ct)))
            .RequireAuthorization("Purchasing.View").WithName("GetPurchaseOrders");

        po.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => Send(s.Send(new GetPurchaseOrderQuery(id), ct)))
            .RequireAuthorization("Purchasing.View").WithName("GetPurchaseOrder");

        po.MapPost("/", (CreatePurchaseOrderCommand c, ISender s, CancellationToken ct) =>
                Created(s.Send(c, ct), "/api/v1/purchase-orders"))
            .RequireAuthorization("Purchasing.Create").WithName("CreatePurchaseOrder");

        po.MapPost("/{id:guid}/submit", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new SubmitPurchaseOrderCommand(id), ct)))
            .RequireAuthorization("Purchasing.Create").WithName("SubmitPurchaseOrder");

        // --- Goods receipts (against a purchase order) ---
        po.MapGet("/{id:guid}/goods-receipts", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetGoodsReceiptsForPurchaseOrderQuery(id), ct)))
            .RequireAuthorization("Purchasing.View").WithName("GetGoodsReceiptsForPurchaseOrder");

        po.MapPost("/{id:guid}/goods-receipts", (Guid id, ReceiveGoodsRequest body, ISender s, CancellationToken ct) =>
                Created(s.Send(new PostGoodsReceiptCommand(id, body.ReceiptDate, body.Notes, body.Lines), ct),
                    $"/api/v1/purchase-orders/{id}/goods-receipts"))
            .RequireAuthorization("Purchasing.Post").WithName("PostGoodsReceipt");

        var gr = app.MapGroup("/api/v1/goods-receipts").WithTags("Purchasing").RequireAuthorization();

        gr.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetGoodsReceiptQuery(id), ct)))
            .RequireAuthorization("Purchasing.View").WithName("GetGoodsReceipt");

        // --- Purchase invoices (against a purchase order) ---
        po.MapGet("/{id:guid}/invoices", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetPurchaseInvoicesForPurchaseOrderQuery(id), ct)))
            .RequireAuthorization("Purchasing.View").WithName("GetPurchaseInvoicesForPurchaseOrder");

        po.MapPost("/{id:guid}/invoices", (Guid id, BillPurchaseRequest body, ISender s, CancellationToken ct) =>
                Created(s.Send(new PostPurchaseInvoiceCommand(
                    id, body.SupplierInvoiceNo, body.InvoiceDate, body.DueDate, body.Notes, body.Lines), ct),
                    $"/api/v1/purchase-orders/{id}/invoices"))
            .RequireAuthorization("Purchasing.Post").WithName("PostPurchaseInvoice");

        var pi = app.MapGroup("/api/v1/purchase-invoices").WithTags("Purchasing").RequireAuthorization();

        pi.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetPurchaseInvoiceQuery(id), ct)))
            .RequireAuthorization("Purchasing.View").WithName("GetPurchaseInvoice");

        // --- Supplier payments ---
        var pay = app.MapGroup("/api/v1/supplier-payments").WithTags("Purchasing").RequireAuthorization();

        pay.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetSupplierPaymentQuery(id), ct)))
            .RequireAuthorization("Purchasing.View").WithName("GetSupplierPayment");

        pay.MapGet("/", (Guid supplierId, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetSupplierPaymentsQuery(supplierId), ct)))
            .RequireAuthorization("Purchasing.View").WithName("GetSupplierPayments");

        pay.MapPost("/", (PostSupplierPaymentCommand c, ISender s, CancellationToken ct) =>
                Created(s.Send(c, ct), "/api/v1/supplier-payments"))
            .RequireAuthorization("Purchasing.Post").WithName("PostSupplierPayment");

        return app;
    }

    public sealed record ReceiveGoodsRequest(
        DateOnly ReceiptDate, string? Notes, IReadOnlyList<GoodsReceiptLineInput> Lines);

    public sealed record BillPurchaseRequest(
        string? SupplierInvoiceNo, DateOnly InvoiceDate, DateOnly DueDate, string? Notes,
        IReadOnlyList<PurchaseInvoiceLineInput> Lines);

    private static async Task<IResult> Send<T>(Task<Result<T>> task) => (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}
