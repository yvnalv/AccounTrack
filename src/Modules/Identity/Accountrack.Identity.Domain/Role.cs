using Accountrack.SharedKernel.Domain;

namespace Accountrack.Identity.Domain;

/// <summary>
/// A tenant-scoped collection of permissions assignable to users (RBAC — ADR-0019).
/// System roles are seeded per tenant and cannot be deleted.
/// </summary>
public sealed class Role : TenantScopedEntity, IAggregateRoot
{
    private readonly List<RolePermission> _permissions = new();

    private Role() { }

    public Role(Guid tenantId, string name, string? description = null, bool isSystem = false)
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
        IsSystem = isSystem;
    }

    public string Name { get; private set; } = default!;

    public string? Description { get; private set; }

    public bool IsSystem { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    public void GrantPermission(Guid permissionId)
    {
        if (_permissions.All(p => p.PermissionId != permissionId))
        {
            _permissions.Add(new RolePermission(Id, permissionId));
        }
    }

    public void RevokePermission(Guid permissionId) =>
        _permissions.RemoveAll(p => p.PermissionId == permissionId);

    public void Rename(string name, string? description)
    {
        Name = name.Trim();
        Description = description?.Trim();
    }

    /// <summary>Replaces the role's permission set with exactly the given permission ids.</summary>
    public void ReplacePermissions(IEnumerable<Guid> permissionIds)
    {
        var target = permissionIds.Distinct().ToList();
        _permissions.RemoveAll(p => !target.Contains(p.PermissionId));
        foreach (var id in target)
        {
            GrantPermission(id);
        }
    }
}

/// <summary>Join entity linking a <see cref="Role"/> to a <see cref="Permission"/>.</summary>
public sealed class RolePermission : Entity
{
    private RolePermission() { }

    public RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }
}
