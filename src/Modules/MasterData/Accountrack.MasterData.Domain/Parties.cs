using Accountrack.SharedKernel.Domain;

namespace Accountrack.MasterData.Domain;

/// <summary>A customer (sales trading partner).</summary>
public sealed class Customer : TenantOwnedEntity, IAggregateRoot, IHasCode
{
    private Customer() { }

    private Customer(string code, string name)
    {
        Code = code;
        Name = name;
        IsActive = true;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? TaxId { get; private set; }
    public int PaymentTermDays { get; private set; }
    public decimal CreditLimit { get; private set; }
    public bool IsActive { get; private set; }

    public static Customer Create(string code, string name, string? taxId, int paymentTermDays, decimal creditLimit)
    {
        if (paymentTermDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermDays));
        }

        return new Customer(code.Trim().ToUpperInvariant(), name.Trim())
        {
            TaxId = taxId?.Trim(),
            PaymentTermDays = paymentTermDays,
            CreditLimit = creditLimit,
        };
    }

    public void Deactivate() => IsActive = false;
}

/// <summary>A supplier (purchasing trading partner).</summary>
public sealed class Supplier : TenantOwnedEntity, IAggregateRoot, IHasCode
{
    private Supplier() { }

    private Supplier(string code, string name)
    {
        Code = code;
        Name = name;
        IsActive = true;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? TaxId { get; private set; }
    public int PaymentTermDays { get; private set; }
    public bool IsActive { get; private set; }

    public static Supplier Create(string code, string name, string? taxId, int paymentTermDays)
    {
        if (paymentTermDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentTermDays));
        }

        return new Supplier(code.Trim().ToUpperInvariant(), name.Trim())
        {
            TaxId = taxId?.Trim(),
            PaymentTermDays = paymentTermDays,
        };
    }

    public void Deactivate() => IsActive = false;
}
