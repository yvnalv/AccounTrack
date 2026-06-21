using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// Role persistence. Roles are tenant-scoped, so reads run under the ambient tenant query filter
/// (an admin manages only their own tenant's roles). The permission catalog is tenant-independent.
/// </summary>
public sealed class RoleRepository : IRoleRepository
{
    private readonly IdentityDbContext _db;

    public RoleRepository(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken ct) =>
        await _db.Roles
            .Include(r => r.Permissions)
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .ToListAsync(ct);

    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> NameExistsAsync(string name, Guid? excludingRoleId, CancellationToken ct) =>
        _db.Roles.AnyAsync(r => r.Name == name && (excludingRoleId == null || r.Id != excludingRoleId), ct);

    public Task<int> CountUsersAsync(Guid roleId, CancellationToken ct) =>
        _db.UserRoles.CountAsync(ur => ur.RoleId == roleId, ct);

    public async Task<IReadOnlyDictionary<string, Guid>> GetPermissionIdByCodeAsync(CancellationToken ct) =>
        await _db.Permissions.IgnoreQueryFilters().ToDictionaryAsync(p => p.Code, p => p.Id, ct);

    public async Task<IReadOnlyList<Permission>> ListPermissionsAsync(CancellationToken ct) =>
        await _db.Permissions.IgnoreQueryFilters().OrderBy(p => p.Code).ToListAsync(ct);

    public void Add(Role role) => _db.Roles.Add(role);

    public void Remove(Role role) => _db.Roles.Remove(role);
}
