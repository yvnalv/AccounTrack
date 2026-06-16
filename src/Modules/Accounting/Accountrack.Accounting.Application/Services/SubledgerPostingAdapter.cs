using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Domain;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Accounting.Application.Services;

/// <summary>
/// Adapter exposing AR/AP open-item creation as the public <see cref="ISubledgerPosting"/> contract,
/// resolving the company's functional currency so other modules don't depend on Accounting internals.
/// Save-less — the caller's unit of work commits the open item with the journal.
/// </summary>
public sealed class SubledgerPostingAdapter : ISubledgerPosting
{
    private readonly ISubledgerService _subledger;
    private readonly ICompanyDirectory _companies;
    private readonly ITenantContext _tenant;

    public SubledgerPostingAdapter(ISubledgerService subledger, ICompanyDirectory companies, ITenantContext tenant)
    {
        _subledger = subledger;
        _companies = companies;
        _tenant = tenant;
    }

    public Task<Result<Guid>> OpenPayableAsync(
        Guid supplierId, Guid sourceDocumentId, string documentNo, DateOnly documentDate, DateOnly dueDate,
        decimal amount, CancellationToken ct) =>
        OpenAsync(SubledgerType.Payable, JournalSource.PurchaseInvoice, supplierId, sourceDocumentId,
            documentNo, documentDate, dueDate, amount, ct);

    public Task<Result<Guid>> OpenReceivableAsync(
        Guid customerId, Guid sourceDocumentId, string documentNo, DateOnly documentDate, DateOnly dueDate,
        decimal amount, CancellationToken ct) =>
        OpenAsync(SubledgerType.Receivable, JournalSource.SalesInvoice, customerId, sourceDocumentId,
            documentNo, documentDate, dueDate, amount, ct);

    public Task<Result<Guid>> AllocateAsync(
        Guid openItemId, string paymentReference, DateOnly date, decimal amount, Guid paymentDocumentId,
        CancellationToken ct) =>
        _subledger.AllocateAsync(openItemId, paymentReference, date, amount, paymentDocumentId, ct);

    private async Task<Result<Guid>> OpenAsync(
        SubledgerType type, JournalSource source, Guid partyId, Guid sourceDocumentId,
        string documentNo, DateOnly documentDate, DateOnly dueDate, decimal amount, CancellationToken ct)
    {
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        if (company is null)
        {
            return Error.NotFound("ACCOUNTING.COMPANY_NOT_FOUND", "Active company not found.");
        }

        return await _subledger.OpenItemAsync(
            type, partyId, source, sourceDocumentId, documentNo, documentDate, dueDate, amount,
            company.FunctionalCurrency, ct);
    }
}
