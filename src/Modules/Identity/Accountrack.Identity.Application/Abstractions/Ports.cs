using Accountrack.Identity.Domain;

namespace Accountrack.Identity.Application.Abstractions;

/// <summary>Hashes and verifies passwords (PBKDF2/Argon2 — SECURITY.md §1). Implemented in Infrastructure.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string providedPassword);
}

/// <summary>The identity claims that go into an access token.</summary>
public sealed record TokenSubject(
    Guid UserId,
    Guid TenantId,
    string Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<Guid> CompanyIds);

public sealed record AccessTokenDescriptor(string Value, DateTime ExpiresAtUtc);

public sealed record RefreshTokenDescriptor(string RawValue, string Hash, DateTime ExpiresAtUtc);

/// <summary>Issues JWT access tokens and rotating refresh tokens (ADR-0020). Implemented in Infrastructure.</summary>
public interface ITokenService
{
    AccessTokenDescriptor GenerateAccessToken(TokenSubject subject);

    RefreshTokenDescriptor GenerateRefreshToken();

    string HashRefreshToken(string rawValue);
}

/// <summary>A user's authorization snapshot used to build tokens.</summary>
public sealed record UserAuthData(
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<Guid> CompanyIds);

public interface IUserRepository
{
    /// <summary>Looks up a user by email across all tenants (reviewed auth path; bypasses the tenant filter).</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);

    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<bool> EmailExistsAsync(string email, CancellationToken ct);

    Task<UserAuthData> GetAuthDataAsync(Guid userId, CancellationToken ct);

    void Add(User user);
}

public interface IRoleRepository
{
    Task<IReadOnlyList<Role>> ListAsync(CancellationToken ct);

    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<bool> NameExistsAsync(string name, Guid? excludingRoleId, CancellationToken ct);

    /// <summary>How many users in the tenant are assigned this role (for the delete guard).</summary>
    Task<int> CountUsersAsync(Guid roleId, CancellationToken ct);

    /// <summary>All catalog permissions (code → row id), tenant-independent.</summary>
    Task<IReadOnlyDictionary<string, Guid>> GetPermissionIdByCodeAsync(CancellationToken ct);

    /// <summary>All catalog permissions as (id, code, name) for display.</summary>
    Task<IReadOnlyList<Permission>> ListPermissionsAsync(CancellationToken ct);

    void Add(Role role);

    void Remove(Role role);
}

public interface IRefreshTokenRepository
{
    /// <summary>Looks up a refresh token by its hash (reviewed auth path; bypasses the tenant filter).</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct);

    /// <summary>All not-yet-revoked tokens in a family (for reuse-detection revocation).</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveFamilyAsync(Guid familyId, CancellationToken ct);

    void Add(RefreshToken token);
}

/// <summary>Persists the Identity unit of work.</summary>
public interface IIdentityUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
