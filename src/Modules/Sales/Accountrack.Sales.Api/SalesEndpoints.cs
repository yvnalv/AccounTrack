using Accountrack.Sales.Application.Features;
using Accountrack.SharedKernel.Results;
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

        so.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => Send(s.Send(new GetSalesOrderQuery(id), ct)))
            .RequireAuthorization("Sales.View").WithName("GetSalesOrder");

        so.MapPost("/", (CreateSalesOrderCommand c, ISender s, CancellationToken ct) =>
                Created(s.Send(c, ct), "/api/v1/sales-orders"))
            .RequireAuthorization("Sales.Create").WithName("CreateSalesOrder");

        so.MapPost("/{id:guid}/submit", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new SubmitSalesOrderCommand(id), ct)))
            .RequireAuthorization("Sales.Create").WithName("SubmitSalesOrder");

        return app;
    }

    private static async Task<IResult> Send<T>(Task<Result<T>> task) => (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}
