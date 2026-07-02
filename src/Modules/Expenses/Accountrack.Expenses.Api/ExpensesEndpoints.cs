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

        // One-shot "save & post": records and (auto-)posts a voucher in a single call (BR-EXP-5).
        vouchers.MapPost("/", (PostExpenseVoucherCommand c, ISender s, CancellationToken ct) =>
                Created(s.Send(c, ct), "/api/v1/expense-vouchers"))
            .RequireAuthorization("Expenses.Post").WithName("PostExpenseVoucher");

        // Draft workflow (parity with Sales/Purchasing): create → edit → submit, or cancel a draft.
        vouchers.MapPost("/draft", (CreateExpenseDraftCommand c, ISender s, CancellationToken ct) =>
                Created(s.Send(c, ct), "/api/v1/expense-vouchers"))
            .RequireAuthorization("Expenses.Create").WithName("CreateExpenseDraft");

        vouchers.MapPut("/{id:guid}", (Guid id, UpdateExpenseVoucherBody b, ISender s, CancellationToken ct) =>
                SendResult(s.Send(new UpdateExpenseVoucherCommand(
                    id, b.ExpenseDate, b.PayeeName, b.CashAccountId, b.SupplierId, b.DueDate, b.Reference, b.Notes,
                    b.Lines, b.RowVersion), ct)))
            .RequireAuthorization("Expenses.Edit").WithName("UpdateExpenseVoucher");

        vouchers.MapPost("/{id:guid}/submit", (Guid id, ISender s, CancellationToken ct) =>
                Send(s.Send(new SubmitExpenseVoucherCommand(id), ct)))
            .RequireAuthorization("Expenses.Post").WithName("SubmitExpenseVoucher");

        vouchers.MapPost("/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) =>
                SendResult(s.Send(new CancelExpenseVoucherCommand(id), ct)))
            .RequireAuthorization("Expenses.Cancel").WithName("CancelExpenseVoucher");

        // Reversal of a posted voucher (posted docs are immutable; correct by reversal — BR-EXP-4).
        vouchers.MapPost("/{id:guid}/reverse", (Guid id, ReverseExpenseVoucherBody b, ISender s, CancellationToken ct) =>
                Send(s.Send(new ReverseExpenseVoucherCommand(id, b.Date, b.Reason), ct)))
            .RequireAuthorization("Expenses.Post").WithName("ReverseExpenseVoucher");

        return app;
    }

    private static async Task<IResult> Send<T>(Task<Result<T>> task) => (await task).ToHttpResult();

    private static async Task<IResult> SendResult(Task<Result> task) => (await task).ToHttpResult();

    private static async Task<IResult> Created<T>(Task<Result<T>> task, string location) =>
        (await task).ToCreatedResult(location);
}

public sealed record UpdateExpenseCategoryBody(string Name, string PostingRuleKey);
public sealed record SetActiveBody(bool IsActive);

public sealed record UpdateExpenseVoucherBody(
    DateOnly ExpenseDate, string? PayeeName, Guid? CashAccountId, Guid? SupplierId, DateOnly? DueDate,
    string? Reference, string? Notes, IReadOnlyList<ExpenseVoucherLineInput> Lines, byte[]? RowVersion);

public sealed record ReverseExpenseVoucherBody(DateOnly? Date, string? Reason);
