using Accountrack.Accounting.Application.Abstractions;
using Accountrack.Accounting.Domain;
using Accountrack.SharedKernel.Results;
using Accountrack.SharedKernel.ValueObjects;

namespace Accountrack.Accounting.Application.Services;

/// <summary>
/// Default <see cref="ISubledgerService"/>. Creates AR/AP open items and allocates payments,
/// translating domain guards into <see cref="Result"/> errors. Does not save — the calling use case
/// owns the unit of work (so a future invoice/payment handler can post the journal + move the
/// subledger atomically).
/// </summary>
public sealed class SubledgerService : ISubledgerService
{
    private readonly ISubledgerRepository _items;

    public SubledgerService(ISubledgerRepository items) => _items = items;

    public Task<Result<Guid>> OpenItemAsync(
        SubledgerType type, Guid partyId, JournalSource sourceType, Guid? sourceDocumentId,
        string documentNo, DateOnly documentDate, DateOnly dueDate, decimal amount, string currency,
        CancellationToken ct)
    {
        SubledgerOpenItem item;
        try
        {
            item = SubledgerOpenItem.Open(
                type, partyId, sourceType, sourceDocumentId, documentNo, documentDate, dueDate,
                Money.Create(amount, currency));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult<Result<Guid>>(Error.Validation("ACCOUNTING.OPEN_ITEM_INVALID", ex.Message));
        }

        _items.Add(item);
        return Task.FromResult<Result<Guid>>(item.Id);
    }

    public async Task<Result<Guid>> AllocateAsync(
        Guid openItemId, string paymentReference, DateOnly date, decimal amount, Guid? paymentDocumentId,
        CancellationToken ct)
    {
        var item = await _items.GetByIdAsync(openItemId, ct);
        if (item is null)
        {
            return AccountingErrors.OpenItemNotFound;
        }

        if (item.Status == OpenItemStatus.Settled)
        {
            return AccountingErrors.OpenItemSettled;
        }

        try
        {
            var allocation = item.Allocate(paymentReference, date, Money.Create(amount, item.Currency), paymentDocumentId);
            return allocation.Id;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("exceeds", StringComparison.OrdinalIgnoreCase))
        {
            return AccountingErrors.AllocationExceedsOutstanding;
        }
        catch (InvalidOperationException ex)
        {
            return Error.Validation("ACCOUNTING.ALLOCATION_INVALID", ex.Message);
        }
    }

    public async Task<Result<decimal>> GetOutstandingAsync(Guid openItemId, CancellationToken ct)
    {
        var item = await _items.GetByIdAsync(openItemId, ct);
        if (item is null)
        {
            return AccountingErrors.OpenItemNotFound;
        }

        return item.OutstandingAmount.Round().Amount;
    }
}
