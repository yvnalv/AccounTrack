using Accountrack.SharedKernel.Results;

namespace Accountrack.Modules.Contracts.Accounting;

/// <summary>
/// Public contract for opening an AR/AP subledger item as part of another module's atomic
/// transaction (ACCOUNTING_DESIGN.md §5): a sales invoice opens a receivable, a purchase invoice
/// opens a payable. Save-less — the caller's <c>ICrossModuleUnitOfWork</c> commits it alongside the
/// document and its GL journal, keeping the subledger in step with the control account.
/// </summary>
public interface ISubledgerPosting
{
    Task<Result<Guid>> OpenPayableAsync(
        Guid supplierId, Guid sourceDocumentId, string documentNo, DateOnly documentDate, DateOnly dueDate,
        decimal amount, CancellationToken ct);

    Task<Result<Guid>> OpenReceivableAsync(
        Guid customerId, Guid sourceDocumentId, string documentNo, DateOnly documentDate, DateOnly dueDate,
        decimal amount, CancellationToken ct);
}
