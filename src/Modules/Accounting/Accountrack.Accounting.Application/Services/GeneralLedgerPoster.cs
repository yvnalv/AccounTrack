using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Accounting.Application.Services;

/// <summary>
/// Adapter exposing journal posting as the public <see cref="IGeneralLedgerPoster"/> contract so
/// other modules can post a GL journal inside their atomic transaction (INTEGRATION_EVENTS.md §3).
/// Builds a balanced draft in the company's functional currency and posts it (save-less).
/// </summary>
public sealed class GeneralLedgerPoster : IGeneralLedgerPoster
{
    private readonly IJournalPoster _poster;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;

    public GeneralLedgerPoster(IJournalPoster poster, ICompanyDirectory companies, ITenantContext tenant)
    {
        _poster = poster;
        _companies = companies;
        _tenant = tenant;
    }

    public async Task<Result<Guid>> PostAsync(LedgerPostingRequest request, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("ACCOUNTING.COMPANY_NOT_FOUND", "Active company not found.");
        }

        var draft = JournalEntry.CreateDraft(
            request.Date, company.FunctionalCurrency, MapSource(request.Source), request.SourceDocumentId,
            request.Description.Trim());

        foreach (var line in request.Lines)
        {
            draft.AddLine(line.AccountId, line.Debit, line.Credit, line.Description, line.SubledgerPartyId);
        }

        return await _poster.PostAsync(draft, ct);
    }

    private static JournalSource MapSource(LedgerSource source) => source switch
    {
        LedgerSource.GoodsReceipt => JournalSource.GoodsReceipt,
        LedgerSource.PurchaseInvoice => JournalSource.PurchaseInvoice,
        LedgerSource.SupplierPayment => JournalSource.Payment,
        LedgerSource.SalesInvoice => JournalSource.SalesInvoice,
        LedgerSource.CustomerPayment => JournalSource.Payment,
        LedgerSource.Shipment => JournalSource.Shipment,
        LedgerSource.StockAdjustment => JournalSource.StockAdjustment,
        _ => JournalSource.Manual,
    };
}
