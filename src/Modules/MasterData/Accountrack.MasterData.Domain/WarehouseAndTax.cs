using Accountrack.SharedKernel.Domain;

namespace Accountrack.MasterData.Domain;

/// <summary>A stock location.</summary>
public sealed class Warehouse : TenantOwnedEntity, IAggregateRoot, IHasCode
{
    private Warehouse() { }

    private Warehouse(string code, string name)
    {
        Code = code;
        Name = name;
        IsActive = true;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Address { get; private set; }
    public bool IsActive { get; private set; }

    public static Warehouse Create(string code, string name, string? address = null) =>
        new(code.Trim().ToUpperInvariant(), name.Trim()) { Address = address?.Trim() };

    public void Deactivate() => IsActive = false;
}

/// <summary>
/// A tax code (e.g. Indonesian PPN at 11%). The rate is fractional (0.11). Account determination
/// for the tax lives in posting rules (ADR-0012/0024).
/// </summary>
public sealed class TaxCode : TenantOwnedEntity, IAggregateRoot, IHasCode
{
    private TaxCode() { }

    private TaxCode(string code, string name, decimal rate)
    {
        Code = code;
        Name = name;
        Rate = rate;
        IsActive = true;
    }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;

    /// <summary>Fractional rate, e.g. 0.11 for PPN 11%.</summary>
    public decimal Rate { get; private set; }

    public bool IsActive { get; private set; }

    public static TaxCode Create(string code, string name, decimal rate)
    {
        if (rate is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "Rate must be a fraction between 0 and 1.");
        }

        return new TaxCode(code.Trim().ToUpperInvariant(), name.Trim(), rate);
    }

    public void Deactivate() => IsActive = false;
}
