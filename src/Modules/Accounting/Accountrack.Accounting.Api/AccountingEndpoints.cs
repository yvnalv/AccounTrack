using Accountrack.Accounting.Application.Features;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Accounting.Api;

public static class AccountingEndpoints
{
    public sealed record ReverseRequest(DateOnly? Date, string? Reason);

    public static IEndpointRouteBuilder MapAccountingEndpoints(this IEndpointRouteBuilder app)
    {
        // --- Chart of Accounts ---
        var accounts = app.MapGroup("/api/v1/accounts").WithTags("Chart of Accounts").RequireAuthorization();

        accounts.MapGet("/", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetAccountsQuery(), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.View").WithName("GetAccounts");

        accounts.MapPost("/", async (CreateAccountCommand cmd, ISender sender, CancellationToken ct) =>
            (await sender.Send(cmd, ct)).ToCreatedResult("/api/v1/accounts"))
            .RequireAuthorization("MasterData.Manage").WithName("CreateAccount");

        // --- Fiscal years & periods ---
        var fiscal = app.MapGroup("/api/v1/fiscal-years").WithTags("Fiscal Periods").RequireAuthorization();

        fiscal.MapGet("/", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetFiscalYearsQuery(), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.View").WithName("GetFiscalYears");

        fiscal.MapPost("/", async (CreateFiscalYearCommand cmd, ISender sender, CancellationToken ct) =>
            (await sender.Send(cmd, ct)).ToCreatedResult("/api/v1/fiscal-years"))
            .RequireAuthorization("Accounting.Post").WithName("CreateFiscalYear");

        var periods = app.MapGroup("/api/v1/fiscal-periods").WithTags("Fiscal Periods").RequireAuthorization();

        periods.MapPost("/{id:guid}/close", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CloseFiscalPeriodCommand(id), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.PeriodClose").WithName("CloseFiscalPeriod");

        periods.MapPost("/{id:guid}/reopen", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ReopenFiscalPeriodCommand(id), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.PeriodReopen").WithName("ReopenFiscalPeriod");

        // --- Journals ---
        var journals = app.MapGroup("/api/v1/journal-entries").WithTags("Journals").RequireAuthorization();

        journals.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetJournalEntryQuery(id), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.View").WithName("GetJournalEntry");

        journals.MapPost("/", async (PostJournalCommand cmd, ISender sender, CancellationToken ct) =>
            (await sender.Send(cmd, ct)).ToCreatedResult("/api/v1/journal-entries"))
            .RequireAuthorization("Accounting.Post").WithName("PostJournal");

        journals.MapPost("/{id:guid}/reverse", async (Guid id, ReverseRequest body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ReverseJournalCommand(id, body.Date, body.Reason), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.Post").WithName("ReverseJournal");

        // --- Reports ---
        var reports = app.MapGroup("/api/v1/reports").WithTags("Reports").RequireAuthorization("Accounting.View");

        reports.MapGet("/trial-balance", async (DateOnly? fromDate, DateOnly? toDate, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetTrialBalanceQuery(fromDate, toDate), ct)).ToHttpResult())
            .WithName("GetTrialBalance");

        reports.MapGet("/profit-loss", async (DateOnly? fromDate, DateOnly? toDate, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetProfitAndLossQuery(fromDate, toDate), ct)).ToHttpResult())
            .WithName("GetProfitAndLoss");

        reports.MapGet("/balance-sheet", async (DateOnly? asOfDate, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetBalanceSheetQuery(asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct)).ToHttpResult())
            .WithName("GetBalanceSheet");

        // --- Posting rules (account determination) ---
        var postingRules = app.MapGroup("/api/v1/posting-rules").WithTags("Posting Rules").RequireAuthorization();

        postingRules.MapGet("/", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetPostingRulesQuery(), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.View").WithName("GetPostingRules");

        postingRules.MapGet("/health", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetPostingRuleHealthQuery(), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.View").WithName("GetPostingRuleHealth");

        postingRules.MapPost("/", async (SetPostingRuleCommand cmd, ISender sender, CancellationToken ct) =>
            (await sender.Send(cmd, ct)).ToCreatedResult("/api/v1/posting-rules"))
            .RequireAuthorization("Accounting.Post").WithName("SetPostingRule");

        return app;
    }
}
