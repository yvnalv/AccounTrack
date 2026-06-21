namespace Accountrack.Identity.Application.Contracts;

/// <summary>The token pair and user summary returned by login/refresh.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<Guid> CompanyIds);

/// <summary>A role with its permission codes, for the Settings role manager.</summary>
public sealed record RoleDto(
    Guid Id, string Name, string? Description, bool IsSystem, bool IsAdministrator, int UserCount,
    IReadOnlyList<string> Permissions);

/// <summary>A catalog permission with its module group, for the permission matrix.</summary>
public sealed record PermissionDto(string Code, string Name, string Module);
