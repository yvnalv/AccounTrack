using Accountrack.SharedKernel.Domain;
using Accountrack.SharedKernel.ValueObjects;

namespace Accountrack.Accounting.Domain;

/// <summary>
/// An open item in the AR or AP subledger (ADR-0011): one outstanding amount owed by a customer
/// (Receivable) or to a supplier (Payable), tracked from its source document until fully settled by
/// payment allocations. Subledger outstanding must reconcile to the GL control account
/// (docs/ACCOUNTING_DESIGN.md §5).
/// </summary>
public sealed class SubledgerOpenItem : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<SubledgerAllocation> _allocations = new();

    private SubledgerOpenItem() { }

    private SubledgerOpenItem(
        SubledgerType type, Guid partyId, JournalSource sourceType, Guid? sourceDocumentId,
        string documentNo, DateOnly documentDate, DateOnly dueDate, Money originalAmount)
    {
        Type = type;
        PartyId = partyId;
        SourceType = sourceType;
        SourceDocumentId = sourceDocumentId;
        DocumentNo = documentNo;
        DocumentDate = documentDate;
        DueDate = dueDate;
        Currency = originalAmount.Currency;
        OriginalAmount = originalAmount;
        SettledAmount = Money.Zero(originalAmount.Currency);
        Status = OpenItemStatus.Open;
    }

    public SubledgerType Type { get; private set; }
    public Guid PartyId { get; private set; }
    public JournalSource SourceType { get; private set; }
    public Guid? SourceDocumentId { get; private set; }
    public string DocumentNo { get; private set; } = default!;
    public DateOnly DocumentDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public string Currency { get; private set; } = default!;
    public Money OriginalAmount { get; private set; } = default!;
    public Money SettledAmount { get; private set; } = default!;
    public OpenItemStatus Status { get; private set; }

    public IReadOnlyCollection<SubledgerAllocation> Allocations => _allocations.AsReadOnly();

    public Money OutstandingAmount => OriginalAmount.Subtract(SettledAmount);

    public static SubledgerOpenItem Open(
        SubledgerType type, Guid partyId, JournalSource sourceType, Guid? sourceDocumentId,
        string documentNo, DateOnly documentDate, DateOnly dueDate, Money originalAmount)
    {
        if (originalAmount.Amount <= 0)
        {
            throw new InvalidOperationException("An open item's original amount must be positive.");
        }

        return new SubledgerOpenItem(
            type, partyId, sourceType, sourceDocumentId, documentNo.Trim(), documentDate, dueDate, originalAmount);
    }

    /// <summary>Allocates a payment amount to this item, reducing the outstanding balance (BR-ACC-7).</summary>
    public SubledgerAllocation Allocate(string paymentReference, DateOnly date, Money amount, Guid? paymentDocumentId = null)
    {
        if (Status == OpenItemStatus.Settled)
        {
            throw new InvalidOperationException("Cannot allocate to a settled open item.");
        }

        if (amount.Currency != Currency)
        {
            throw new InvalidOperationException("Allocation currency must match the open item currency.");
        }

        if (amount.Amount <= 0)
        {
            throw new InvalidOperationException("Allocation amount must be positive.");
        }

        if (amount.Round().Amount > OutstandingAmount.Round().Amount)
        {
            throw new InvalidOperationException("Allocation exceeds the outstanding amount.");
        }

        var allocation = SubledgerAllocation.Create(Id, paymentReference, date, amount, paymentDocumentId);
        _allocations.Add(allocation);

        SettledAmount = SettledAmount.Add(amount);
        Status = OutstandingAmount.Round().IsZero ? OpenItemStatus.Settled : OpenItemStatus.PartiallyPaid;

        return allocation;
    }
}

/// <summary>A single payment allocation against a subledger open item.</summary>
public sealed class SubledgerAllocation : Entity
{
    private SubledgerAllocation() { }

    private SubledgerAllocation(Guid openItemId, string paymentReference, DateOnly date, Money amount, Guid? paymentDocumentId)
    {
        OpenItemId = openItemId;
        PaymentReference = paymentReference;
        Date = date;
        Amount = amount;
        PaymentDocumentId = paymentDocumentId;
    }

    public Guid OpenItemId { get; private set; }
    public string PaymentReference { get; private set; } = default!;
    public DateOnly Date { get; private set; }
    public Money Amount { get; private set; } = default!;
    public Guid? PaymentDocumentId { get; private set; }

    internal static SubledgerAllocation Create(
        Guid openItemId, string paymentReference, DateOnly date, Money amount, Guid? paymentDocumentId) =>
        new(openItemId, paymentReference.Trim(), date, amount, paymentDocumentId);
}
