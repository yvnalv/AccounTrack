using Accountrack.Inventory.Application.Features;
using Accountrack.SharedKernel.Results;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Inventory.Api;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var stock = app.MapGroup("/api/v1/stock").WithTags("Inventory").RequireAuthorization();

        stock.MapPost("/receipts", (ReceiveStockCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct)))
            .RequireAuthorization("Inventory.Adjust").WithName("ReceiveStock");

        stock.MapPost("/adjustments", (AdjustStockCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct)))
            .RequireAuthorization("Inventory.Adjust").WithName("AdjustStock");

        stock.MapPost("/transfers", (TransferStockCommand c, ISender s, CancellationToken ct) => Created(s.Send(c, ct)))
            .RequireAuthorization("Inventory.Transfer").WithName("TransferStock");

        stock.MapGet("/on-hand", (ISender s, CancellationToken ct) => Send(s.Send(new GetStockOnHandQuery(), ct)))
            .RequireAuthorization("Inventory.View").WithName("GetStockOnHand");

        stock.MapGet("/card", (Guid productId, Guid? warehouseId, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetStockCardQuery(productId, warehouseId), ct)))
            .RequireAuthorization("Inventory.View").WithName("GetStockCard");

        return app;
    }

    private static async Task<IResult> Send<T>(Task<Result<T>> task) => (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<Result<T>> task) =>
        (await task).ToCreatedResult("/api/v1/stock");
}
