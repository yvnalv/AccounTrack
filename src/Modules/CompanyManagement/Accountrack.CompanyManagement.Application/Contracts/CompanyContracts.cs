namespace Accountrack.CompanyManagement.Application.Contracts;

/// <summary>Company summary/detail returned by the API.</summary>
public sealed record CompanyDto(
    Guid Id,
    string Code,
    string Name,
    string? LegalName,
    string FunctionalCurrency,
    int FiscalYearStartMonth,
    string TimeZone,
    string? TaxId,
    bool IsVatRegistered,
    bool IsActive);
