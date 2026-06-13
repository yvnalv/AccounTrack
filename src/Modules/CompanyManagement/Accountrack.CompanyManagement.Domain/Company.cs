using Accountrack.SharedKernel.Domain;

namespace Accountrack.CompanyManagement.Domain;

/// <summary>
/// A legal entity / operational business unit within a tenant. Books (GL, inventory) are kept
/// per company. Tenant-scoped (carries TenantId; its own Id is the CompanyId other modules use).
/// </summary>
public sealed class Company : TenantScopedEntity, IAggregateRoot
{
    private Company() { }

    private Company(
        Guid id,
        Guid tenantId,
        string code,
        string name,
        string functionalCurrency,
        int fiscalYearStartMonth,
        string timeZone) : base(id)
    {
        TenantId = tenantId;
        Code = code;
        Name = name;
        FunctionalCurrency = functionalCurrency;
        FiscalYearStartMonth = fiscalYearStartMonth;
        TimeZone = timeZone;
        IsActive = true;
    }

    public string Code { get; private set; } = default!;

    public string Name { get; private set; } = default!;

    public string? LegalName { get; private set; }

    /// <summary>The single functional/base currency for this company (ADR-0013), ISO-4217.</summary>
    public string FunctionalCurrency { get; private set; } = default!;

    /// <summary>First month of the fiscal year (1-12). Drives fiscal periods (ADR-0010).</summary>
    public int FiscalYearStartMonth { get; private set; }

    public string TimeZone { get; private set; } = default!;

    /// <summary>Tax identifier (e.g. Indonesian NPWP). Optional in MVP.</summary>
    public string? TaxId { get; private set; }

    public bool IsActive { get; private set; }

    public static Company Create(
        Guid tenantId,
        string code,
        string name,
        string functionalCurrency,
        int fiscalYearStartMonth = 1,
        string timeZone = "Asia/Jakarta")
    {
        ValidateCurrency(functionalCurrency);
        ValidateMonth(fiscalYearStartMonth);

        return new Company(
            Guid.NewGuid(), tenantId, code.Trim(), name.Trim(),
            functionalCurrency.Trim().ToUpperInvariant(), fiscalYearStartMonth, timeZone);
    }

    /// <summary>Creates a company with a specific id (used by seeding / provisioning).</summary>
    public static Company CreateWithId(
        Guid id,
        Guid tenantId,
        string code,
        string name,
        string functionalCurrency,
        int fiscalYearStartMonth = 1,
        string timeZone = "Asia/Jakarta")
    {
        ValidateCurrency(functionalCurrency);
        ValidateMonth(fiscalYearStartMonth);

        return new Company(
            id, tenantId, code.Trim(), name.Trim(),
            functionalCurrency.Trim().ToUpperInvariant(), fiscalYearStartMonth, timeZone);
    }

    public void UpdateProfile(string name, string? legalName, string? taxId, string timeZone)
    {
        Name = name.Trim();
        LegalName = legalName?.Trim();
        TaxId = taxId?.Trim();
        TimeZone = timeZone;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static void ValidateCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new ArgumentException("Functional currency must be a 3-letter ISO-4217 code.", nameof(currency));
        }
    }

    private static void ValidateMonth(int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Fiscal year start month must be 1-12.");
        }
    }
}
