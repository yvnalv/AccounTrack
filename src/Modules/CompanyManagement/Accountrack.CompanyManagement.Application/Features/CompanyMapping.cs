using Accountrack.CompanyManagement.Application.Contracts;
using Accountrack.CompanyManagement.Domain;

namespace Accountrack.CompanyManagement.Application.Features;

internal static class CompanyMapping
{
    public static CompanyDto ToDto(this Company c, bool allowNegativeStock = false) => new(
        c.Id, c.Code, c.Name, c.LegalName, c.FunctionalCurrency,
        c.FiscalYearStartMonth, c.TimeZone, c.TaxId, c.IsVatRegistered, allowNegativeStock, c.IsActive);
}
