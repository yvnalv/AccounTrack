using Accountrack.Accounting.Application.Features;
using Accountrack.Accounting.Domain;
using Accountrack.Web.Common.Pdf;
using Accountrack.Web.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Accountrack.Accounting.Api;

public static class AccountingEndpoints
{
    public sealed record ReverseRequest(DateOnly? Date, string? Reason);

    public sealed record RecordOpenItemRequest(
        Guid PartyId, string DocumentNo, DateOnly DocumentDate, DateOnly DueDate, decimal Amount);

    public sealed record AllocateRequest(string PaymentReference, DateOnly Date, decimal Amount);

    public sealed record UpdateAccountBody(string Name, bool AllowPosting);

    public sealed record SetActiveBody(bool IsActive);

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

        accounts.MapPut("/{id:guid}", async (Guid id, UpdateAccountBody body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new UpdateAccountCommand(id, body.Name, body.AllowPosting), ct)).ToHttpResult())
            .RequireAuthorization("MasterData.Manage").WithName("UpdateAccount");

        accounts.MapPut("/{id:guid}/active", async (Guid id, SetActiveBody body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new SetAccountActiveCommand(id, body.IsActive), ct)).ToHttpResult())
            .RequireAuthorization("MasterData.Manage").WithName("SetAccountActive");

        // --- Fiscal years & periods ---
        var fiscal = app.MapGroup("/api/v1/fiscal-years").WithTags("Fiscal Periods").RequireAuthorization();

        fiscal.MapGet("/", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetFiscalYearsQuery(), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.View").WithName("GetFiscalYears");

        fiscal.MapPost("/", async (CreateFiscalYearCommand cmd, ISender sender, CancellationToken ct) =>
            (await sender.Send(cmd, ct)).ToCreatedResult("/api/v1/fiscal-years"))
            .RequireAuthorization("Accounting.Post").WithName("CreateFiscalYear");

        fiscal.MapPost("/{id:guid}/close", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CloseFiscalYearCommand(id), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.PeriodClose").WithName("CloseFiscalYear");

        var periods = app.MapGroup("/api/v1/fiscal-periods").WithTags("Fiscal Periods").RequireAuthorization();

        periods.MapPost("/{id:guid}/close", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CloseFiscalPeriodCommand(id), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.PeriodClose").WithName("CloseFiscalPeriod");

        periods.MapPost("/{id:guid}/reopen", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ReopenFiscalPeriodCommand(id), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.PeriodReopen").WithName("ReopenFiscalPeriod");

        periods.MapGet("/{id:guid}/balances", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetPeriodBalancesQuery(id), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.View").WithName("GetPeriodBalances");

        periods.MapPost("/{id:guid}/balances/rebuild", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new RebuildPeriodBalancesCommand(id), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.PeriodClose").WithName("RebuildPeriodBalances");

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

        reports.MapGet("/vat", async (DateOnly? fromDate, DateOnly? toDate, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetVatReportQuery(fromDate, toDate), ct)).ToHttpResult())
            .WithName("GetVatReport");

        reports.MapGet("/cash-flow", async (DateOnly? fromDate, DateOnly? toDate, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetCashFlowStatementQuery(fromDate, toDate), ct)).ToHttpResult())
            .WithName("GetCashFlowStatement");

        reports.MapGet("/general-ledger", async (Guid? accountId, DateOnly? fromDate, DateOnly? toDate, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetGeneralLedgerQuery(accountId, fromDate, toDate), ct)).ToHttpResult())
            .WithName("GetGeneralLedger");

        // --- Report PDFs (ADR-0031) ---
        reports.MapGet("/trial-balance/pdf", (DateOnly? fromDate, DateOnly? toDate, ISender s, CancellationToken ct) =>
                PdfRenderer.ReportFile(s.Send(new GetTrialBalancePdfQuery(fromDate, toDate), ct), "trial-balance"))
            .WithName("GetTrialBalancePdf");

        reports.MapGet("/profit-loss/pdf", (DateOnly? fromDate, DateOnly? toDate, ISender s, CancellationToken ct) =>
                PdfRenderer.ReportFile(s.Send(new GetProfitAndLossPdfQuery(fromDate, toDate), ct), "profit-loss"))
            .WithName("GetProfitAndLossPdf");

        reports.MapGet("/balance-sheet/pdf", (DateOnly? asOfDate, ISender s, CancellationToken ct) =>
                PdfRenderer.ReportFile(s.Send(new GetBalanceSheetPdfQuery(asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct), "balance-sheet"))
            .WithName("GetBalanceSheetPdf");

        reports.MapGet("/vat/pdf", (DateOnly? fromDate, DateOnly? toDate, ISender s, CancellationToken ct) =>
                PdfRenderer.ReportFile(s.Send(new GetVatReportPdfQuery(fromDate, toDate), ct), "vat-report"))
            .WithName("GetVatReportPdf");

        reports.MapGet("/cash-flow/pdf", (DateOnly? fromDate, DateOnly? toDate, ISender s, CancellationToken ct) =>
                PdfRenderer.ReportFile(s.Send(new GetCashFlowPdfQuery(fromDate, toDate), ct), "cash-flow"))
            .WithName("GetCashFlowStatementPdf");

        reports.MapGet("/general-ledger/pdf", (Guid? accountId, DateOnly? fromDate, DateOnly? toDate, ISender s, CancellationToken ct) =>
                PdfRenderer.ReportFile(s.Send(new GetGeneralLedgerPdfQuery(accountId, fromDate, toDate), ct), "general-ledger"))
            .WithName("GetGeneralLedgerPdf");

        // --- Dashboard ---
        app.MapGet("/api/v1/dashboard/summary", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetDashboardSummaryQuery(), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.View").WithTags("Dashboard").WithName("GetDashboardSummary");

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

        // --- AR / AP subledgers ---
        MapSubledger(app, "ar", SubledgerType.Receivable);
        MapSubledger(app, "ap", SubledgerType.Payable);

        return app;
    }

    private static void MapSubledger(IEndpointRouteBuilder app, string prefix, SubledgerType type)
    {
        var group = app.MapGroup($"/api/v1/{prefix}").WithTags(prefix.ToUpperInvariant() + " Subledger").RequireAuthorization();

        group.MapGet("/open-items", async (Guid? partyId, bool? includeSettled, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetOpenItemsQuery(type, partyId, includeSettled ?? false), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.View").WithName($"Get{prefix}OpenItems");

        group.MapGet("/aging", async (DateOnly? asOfDate, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetAgingQuery(type, asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct)).ToHttpResult())
            .RequireAuthorization("Accounting.View").WithName($"Get{prefix}Aging");

        group.MapPost("/open-items", async (RecordOpenItemRequest body, ISender sender, CancellationToken ct) =>
            (await sender.Send(
                new RecordOpenItemCommand(type, body.PartyId, body.DocumentNo, body.DocumentDate, body.DueDate, body.Amount), ct))
                .ToCreatedResult($"/api/v1/{prefix}/open-items"))
            .RequireAuthorization("Accounting.Post").WithName($"Record{prefix}OpenItem");

        group.MapPost("/open-items/{id:guid}/allocations", async (Guid id, AllocateRequest body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new AllocatePaymentCommand(id, body.PaymentReference, body.Date, body.Amount), ct))
                .ToCreatedResult($"/api/v1/{prefix}/open-items/{id}/allocations"))
            .RequireAuthorization("Accounting.Post").WithName($"Allocate{prefix}Payment");
    }
}
