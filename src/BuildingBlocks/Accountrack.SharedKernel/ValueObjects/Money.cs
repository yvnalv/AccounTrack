using Accountrack.SharedKernel.Domain;

namespace Accountrack.SharedKernel.ValueObjects;

/// <summary>
/// Money: a decimal amount plus an explicit ISO-4217 currency code (ADR-0013).
/// Never use floating point for amounts. Arithmetic requires matching currencies.
/// </summary>
public sealed class Money : ValueObject
{
    public const int DefaultScale = 4;

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    /// <summary>ISO-4217 alphabetic code, upper-case (e.g. "IDR").</summary>
    public string Currency { get; }

    public static Money Create(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO-4217 code.", nameof(currency));
        }

        return new Money(amount, currency.Trim().ToUpperInvariant());
    }

    public static Money Zero(string currency) => Create(0m, currency);

    public bool IsZero => Amount == 0m;

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    /// <summary>Rounds to the given number of decimals (default 4) using banker's rounding.</summary>
    public Money Round(int decimals = DefaultScale) =>
        new(Math.Round(Amount, decimals, MidpointRounding.ToEven), Currency);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator *(Money money, decimal factor) => money.Multiply(factor);

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot operate on different currencies: {Currency} vs {other.Currency}.");
        }
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)} {Currency}";
}
