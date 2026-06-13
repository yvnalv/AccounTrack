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
}
