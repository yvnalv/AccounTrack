namespace Accountrack.Modules.Contracts.Company;

/// <summary>Minimal company facts other modules need (e.g. functional currency for posting,
/// name + tax id for document headers).</summary>
public sealed record CompanyInfo(
    Guid Id, string Code, string FunctionalCurrency, int FiscalYearStartMonth,
    string Name = "", string? LegalName = null, string? TaxId = null);

/// <summary>
/// Public contract exposed by the Company Management module for other modules to look up company
/// configuration without depending on its internals (ADR-0007).
/// </summary>
public interface ICompanyDirectory
{
    Task<CompanyInfo?> GetAsync(Guid companyId, CancellationToken ct);
}
