using Accountrack.SharedKernel.Domain;

namespace Accountrack.Identity.Domain;

/// <summary>
/// An application user. Tenant-scoped (a user belongs to exactly one tenant) with email unique
/// across the system, so login can resolve the tenant before a tenant context exists
/// (docs/MULTI_TENANCY.md). Company access is granted explicitly via <see cref="Companies"/>.
/// </summary>
public sealed class User : TenantScopedEntity, IAggregateRoot
{
    private readonly List<UserRole> _roles = new();
    private readonly List<UserCompany> _companies = new();

    private User() { }

    private User(Guid tenantId, string email, string passwordHash, string fullName)
    {
        TenantId = tenantId;
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        IsActive = true;
    }

    /// <summary>Normalized (lower-case) email; unique across the system.</summary>
    public string Email { get; private set; } = default!;

    public string PasswordHash { get; private set; } = default!;

    public string FullName { get; private set; } = default!;

    public bool IsActive { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    public IReadOnlyCollection<UserCompany> Companies => _companies.AsReadOnly();

    public static User Create(Guid tenantId, Email email, string passwordHash, string fullName) =>
        new(tenantId, email.Value, passwordHash, fullName);

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void RecordLogin(DateTime nowUtc) => LastLoginAtUtc = nowUtc;

    public void AssignRole(Guid roleId)
    {
        if (_roles.All(r => r.RoleId != roleId))
        {
            _roles.Add(new UserRole(Id, roleId));
        }
    }

    public void GrantCompany(Guid companyId)
    {
        if (_companies.All(c => c.CompanyId != companyId))
        {
            _companies.Add(new UserCompany(Id, companyId));
        }
    }

    public bool HasCompany(Guid companyId) => _companies.Any(c => c.CompanyId == companyId);
}

/// <summary>Join entity assigning a <see cref="Role"/> to a <see cref="User"/>.</summary>
public sealed class UserRole : Entity
{
    private UserRole() { }

    public UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }
}

/// <summary>Grants a <see cref="User"/> access to a company within the tenant (MULTI_TENANCY.md §1).</summary>
public sealed class UserCompany : Entity
{
    private UserCompany() { }

    public UserCompany(Guid userId, Guid companyId)
    {
        UserId = userId;
        CompanyId = companyId;
    }

    public Guid UserId { get; private set; }

    public Guid CompanyId { get; private set; }
}
