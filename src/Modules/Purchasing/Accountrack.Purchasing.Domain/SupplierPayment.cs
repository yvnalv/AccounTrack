using Accountrack.SharedKernel.Domain;

namespace Accountrack.Purchasing.Domain;

/// <summary>
/// A payment to a supplier (procure-to-pay slice 2). Posting it allocates the amount to AP open
/// items and posts Dr AP control / Cr Cash-Bank atomically (docs/POSTING_RULES.md, ACCOUNTING_DESIGN §5).
/// </summary>
public sealed class SupplierPayment : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<SupplierPaymentAllocation> _allocations = new();

    private SupplierPayment() { }

    private SupplierPayment(
        string number, Guid supplierId, Guid cashAccountId, string currency, DateOnly paymentDate,
        string? reference, string? notes)
    {
        Number = number;
        SupplierId = supplierId;
        CashAccountId = cashAccountId;
        Currency = currency;
        PaymentDate = paymentDate;
        Reference = reference;
        Notes = notes;
    }

    public string Number { get; private set; } = default!;
    public Guid SupplierId { get; private set; }

    /// <summary>The GL cash/bank account credited for this payment.</summary>
    public Guid CashAccountId { get; private set; }

    public string Currency { get; private set; } = default!;
    public DateOnly PaymentDate { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>The GL journal posted for this payment (Dr AP control / Cr Cash-Bank).</summary>
    public Guid? JournalEntryId { get; private set; }

    public IReadOnlyList<SupplierPaymentAllocation> Allocations => _allocations;

    public decimal TotalAmount => _allocations.Sum(a => a.Amount);

    public static SupplierPayment Create(
        string number, Guid supplierId, Guid cashAccountId, string currency, DateOnly paymentDate,
        string? reference, string? notes) =>
        new(number, supplierId, cashAccountId, currency, paymentDate, reference?.Trim(), notes?.Trim());

    public void AddAllocation(Guid apOpenItemId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Payment allocation amount must be positive.");
        }

        _allocations.Add(SupplierPaymentAllocation.Create(apOpenItemId, amount));
    }

    public void SetJournal(Guid journalEntryId) => JournalEntryId = journalEntryId;
}

/// <summary>One allocation of a supplier payment to an AP open item.</summary>
public sealed class SupplierPaymentAllocation : Entity
{
    private SupplierPaymentAllocation() { }

    private SupplierPaymentAllocation(Guid apOpenItemId, decimal amount)
    {
        ApOpenItemId = apOpenItemId;
        Amount = amount;
    }

    public Guid SupplierPaymentId { get; private set; }
    public Guid ApOpenItemId { get; private set; }
    public decimal Amount { get; private set; }

    internal static SupplierPaymentAllocation Create(Guid apOpenItemId, decimal amount) =>
        new(apOpenItemId, amount);
}
