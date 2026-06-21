using Accountrack.SharedKernel.Results;

namespace Accountrack.Identity.Domain;

/// <summary>Domain errors for the Identity module (codes are stable; see ERROR_HANDLING.md).</summary>
public static class IdentityErrors
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("IDENTITY.INVALID_CREDENTIALS", "Email or password is incorrect.");

    public static readonly Error UserInactive =
        Error.Forbidden("IDENTITY.USER_INACTIVE", "This account is disabled.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("IDENTITY.INVALID_REFRESH_TOKEN", "The refresh token is invalid or expired.");

    public static readonly Error EmailAlreadyExists =
        Error.Conflict("IDENTITY.EMAIL_EXISTS", "A user with this email already exists.");

    public static readonly Error CompanyNotGranted =
        Error.Forbidden("IDENTITY.COMPANY_NOT_GRANTED", "You do not have access to the requested company.");

    public static readonly Error RoleNotFound =
        Error.NotFound("IDENTITY.ROLE_NOT_FOUND", "Role not found.");

    public static readonly Error RoleNameExists =
        Error.Conflict("IDENTITY.ROLE_NAME_EXISTS", "A role with this name already exists.");

    public static readonly Error RoleIsSystem =
        Error.Conflict("IDENTITY.ROLE_IS_SYSTEM", "A built-in role cannot be renamed or deleted.");

    public static readonly Error RoleIsAdministrator =
        Error.Conflict("IDENTITY.ROLE_IS_ADMINISTRATOR", "The Administrator role always has full access and cannot be edited.");

    public static readonly Error RoleInUse =
        Error.Conflict("IDENTITY.ROLE_IN_USE", "This role is assigned to one or more users and cannot be deleted.");
}
