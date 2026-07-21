namespace Accountrack.Modules.Contracts.Company;

/// <summary>
/// The identity of a newly provisioned company, passed to every <see cref="ICompanyFoundationSeeder"/>.
/// </summary>
/// <param name="TenantId">Owning tenant.</param>
/// <param name="CompanyId">The company to provision.</param>
/// <param name="FunctionalCurrency">The company's functional currency (ADR-0013).</param>
/// <param name="Year">Fiscal year to open (normally the current year).</param>
/// <param name="FiscalYearStartMonth">First month of the fiscal year (1 = January).</param>
public sealed record CompanyFoundation(
    Guid TenantId, Guid CompanyId, string FunctionalCurrency, int Year, int FiscalYearStartMonth);

/// <summary>
/// Implemented by every module that owns baseline data a company cannot operate without — a chart of
/// accounts, posting rules and an open fiscal period (Accounting); a unit, warehouse and tax code
/// (Master Data); expense categories (Expenses).
/// <para>
/// Each module registers its own implementation, so provisioning stays within module boundaries
/// (ADR-0007, Rule 27) and a new module can join simply by registering one. The caller resolves
/// <c>IEnumerable&lt;ICompanyFoundationSeeder&gt;</c> and runs them in <see cref="Order"/>.
/// </para>
/// <para>
/// <b>Implementations must be idempotent</b>: they run on organization sign-up, on new-company
/// creation, and again from the startup backfill that repairs companies provisioned before this
/// contract existed (BR-CMP-1).
/// </para>
/// </summary>
public interface ICompanyFoundationSeeder
{
    /// <summary>
    /// Run order, ascending. Accounting seeds first (100) because other modules' defaults reference
    /// its posting-rule keys / accounts.
    /// </summary>
    int Order { get; }

    Task SeedAsync(CompanyFoundation company, CancellationToken ct);
}
