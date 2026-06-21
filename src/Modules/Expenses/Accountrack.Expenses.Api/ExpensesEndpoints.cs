using Accountrack.Expenses.Application.Features;
using Accountrack.SharedKernel.Results;
using Accountrack.Web.Common.Export;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Expenses.Api;

public static class ExpensesEndpoints
{
    public static IEndpointRouteBuilder MapExpensesEndpoints(this IEndpointRouteBuilder app)
    {
        var cats = app.MapGroup("/api/v1/expense-categories").WithTags("Expenses").RequireAuthorization();

        cats.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetExpenseCategoriesQuery(), ct)))
            .RequireAuthorization("Expenses.View").WithName("GetExpenseCategories");

        cats.MapPost("/", (CreateExpenseCategoryCommand c, ISender s, CancellationToken ct) =>
                Created(s.Send(c, ct), "/api/v1/expense-categories"))
            .RequireAuthorization("Expenses.Manage").WithName("CreateExpenseCategory");

        cats.MapPut("/{id:guid}", (Guid id, UpdateExpenseCategoryBody b, ISender s, CancellationToken ct) =>
                SendResult(s.Send(new UpdateExpenseCategoryCommand(id, b.Name, b.PostingRuleKey), ct)))
            .RequireAuthorization("Expenses.Manage").WithName("UpdateExpenseCategory");

        cats.MapPut("/{id:guid}/active", (Guid id, SetActiveBody b, ISender s, CancellationToken ct) =>
                SendResult(s.Send(new SetExpenseCategoryActiveCommand(id, b.IsActive), ct)))
            .RequireAuthorization("Expenses.Manage").WithName("SetExpenseCategoryActive");

        var vouchers = app.MapGroup("/api/v1/expense-vouchers").WithTags("Expenses").RequireAuthorization();

        vouchers.MapGet("/", (ISender s, CancellationToken ct) => Send(s.Send(new GetExpenseVouchersQuery(), ct)))
            .RequireAuthorization("Expenses.View").WithName("GetExpenseVouchers");

        vouchers.MapGet("/export", (string? format, ISender s, CancellationToken ct) =>
                TableExport.File(s.Send(new ExportExpenseVouchersQuery(), ct), "expense-vouchers", format))
            .RequireAuthorization("Expenses.View").WithName("ExportExpenseVouchers");

        vouchers.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new GetExpenseVoucherQuery(id), ct)))
            .RequireAuthorization("Expenses.View").WithName("GetExpenseVoucher");

        vouchers.MapPost("/", (PostExpenseVoucherCommand c, ISender s, CancellationToken ct) =>
                Created(s.Send(c, ct), "/api/v1/expense-vouchers"))
            .RequireAuthorization("Expenses.Post").WithName("PostExpenseVoucher");

        return app;
    }

    private static async Task<IResult> Send<T>(Task<Result<T>> task) => (await task).ToHttpResult();

    private static async Task<IResult> SendResult(Task<Result> task) => (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}

public sealed record UpdateExpenseCategoryBody(string Name, string PostingRuleKey);
public sealed record SetActiveBody(bool IsActive);
