using Accountrack.SharedKernel.Domain;

namespace Accountrack.Sales.Domain;

/// <summary>
/// A payment received from a customer (order-to-cash slice 2). Posting it allocates the amount to AR
/// open items and posts Dr Cash-Bank / Cr AR control atomically (docs/POSTING_RULES.md, ACCOUNTING_DESIGN §5).
/// </summary>
public sealed class CustomerPayment : TenantOwnedEntity, IAggregateRoot
{
    private readonly List<CustomerPaymentAllocation> _allocations = new();

    private CustomerPayment() { }

    private CustomerPayment(
        string number, Guid customerId, Guid cashAccountId, string currency, DateOnly paymentDate,
        string? reference, string? notes)
    {
        Number = number;
        CustomerId = customerId;
        CashAccountId = cashAccountId;
        Currency = currency;
        PaymentDate = paymentDate;
        Reference = reference;
        Notes = notes;
    }

    public string Number { get; private set; } = default!;
    public Guid CustomerId { get; private set; }

    /// <summary>The GL cash/bank account debited for this receipt.</summary>
    public Guid CashAccountId { get; private set; }

    public string Currency { get; private set; } = default!;
    public DateOnly PaymentDate { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>The GL journal posted for this payment (Dr Cash-Bank / Cr AR control).</summary>
    public Guid? JournalEntryId { get; private set; }

    public IReadOnlyList<CustomerPaymentAllocation> Allocations => _allocations;

    public decimal TotalAmount => _allocations.Sum(a => a.Amount);

    public static CustomerPayment Create(
        string number, Guid customerId, Guid cashAccountId, string currency, DateOnly paymentDate,
        string? reference, string? notes) =>
        new(number, customerId, cashAccountId, currency, paymentDate, reference?.Trim(), notes?.Trim());

    public void AddAllocation(Guid arOpenItemId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Payment allocation amount must be positive.");
        }

        _allocations.Add(CustomerPaymentAllocation.Create(arOpenItemId, amount));
    }

    public void SetJournal(Guid journalEntryId) => JournalEntryId = journalEntryId;
}

/// <summary>One allocation of a customer payment to an AR open item.</summary>
public sealed class CustomerPaymentAllocation : Entity
{
    private CustomerPaymentAllocation() { }

    private CustomerPaymentAllocation(Guid arOpenItemId, decimal amount)
    {
        ArOpenItemId = arOpenItemId;
        Amount = amount;
    }

    public Guid CustomerPaymentId { get; private set; }
    public Guid ArOpenItemId { get; private set; }
    public decimal Amount { get; private set; }

    internal static CustomerPaymentAllocation Create(Guid arOpenItemId, decimal amount) =>
        new(arOpenItemId, amount);
}
