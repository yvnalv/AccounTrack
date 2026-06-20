using Accountrack.SharedKernel.Results;

namespace Accountrack.Modules.Contracts.Accounting;

/// <summary>What produced a journal posted through the cross-module contract (drill-down source).</summary>
public enum LedgerSource
{
    GoodsReceipt = 0,
    PurchaseInvoice = 1,
    SupplierPayment = 2,
    SalesInvoice = 3,
    CustomerPayment = 4,
    Shipment = 5,
    StockAdjustment = 6,
    SalesReturn = 7,
    PurchaseReturn = 8,
}

/// <summary>A single journal line: debit XOR credit, plus an optional subledger party for control lines.</summary>
public sealed record LedgerLine(
    Guid AccountId, decimal Debit, decimal Credit, string? Description = null, Guid? SubledgerPartyId = null);

/// <summary>A request to post a balanced journal from another module, within its atomic transaction.</summary>
public sealed record LedgerPostingRequest(
    DateOnly Date, LedgerSource Source, Guid? SourceDocumentId, string Description, IReadOnlyList<LedgerLine> Lines);

/// <summary>
/// Public contract for posting a GL journal as part of another module's atomic transaction
/// (INTEGRATION_EVENTS.md §3). Save-less: the entry is validated (balanced, period open, postable
/// accounts) and added to the Accounting context; the caller's unit of work commits it.
/// </summary>
public interface IGeneralLedgerPoster
{
    Task<Result<Guid>> PostAsync(LedgerPostingRequest request, CancellationToken ct);
}
