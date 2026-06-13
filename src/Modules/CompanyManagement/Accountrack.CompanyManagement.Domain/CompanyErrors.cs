using Accountrack.SharedKernel.Results;

namespace Accountrack.CompanyManagement.Domain;

public static class CompanyErrors
{
    public static readonly Error NotFound =
        Error.NotFound("COMPANY.NOT_FOUND", "Company not found.");

    public static readonly Error CodeAlreadyExists =
        Error.Conflict("COMPANY.CODE_EXISTS", "A company with this code already exists in the tenant.");
}
