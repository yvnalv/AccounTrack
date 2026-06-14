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

        return app;
    }

    private static async Task<IResult> Send<T>(Task<Result<T>> task) => (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}
