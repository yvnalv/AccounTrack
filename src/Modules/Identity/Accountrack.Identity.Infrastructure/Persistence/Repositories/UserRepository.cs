using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// User persistence. The lookups here run on the anonymous login/refresh path before a tenant
/// context exists, so they intentionally bypass the tenant query filter
/// (reviewed auth allow-list — MULTI_TENANCY.md §5). Soft-delete is re-applied manually.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _db;

    public UserRepository(IdentityDbContext db) => _db = db;

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) =>
        await _db.Users
            .Include(u => u.Roles)
            .Include(u => u.Companies)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

    public Task<User?> GetWithDetailsAsync(Guid id, CancellationToken ct) =>
        _db.Users
            .Include(u => u.Roles)
            .Include(u => u.Companies)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct) =>
        _db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email && !u.IsDeleted, ct);

    public async Task<UserAuthData> GetAuthDataAsync(Guid userId, CancellationToken ct)
    {
        var roleIds = await _db.UserRoles
            .IgnoreQueryFilters()
            .Where(ur => ur.UserId == userId && !ur.IsDeleted)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        var roles = await _db.Roles
            .IgnoreQueryFilters()
            .Where(r => roleIds.Contains(r.Id) && !r.IsDeleted)
            .Select(r => r.Name)
            .ToListAsync(ct);

        var permissionIds = await _db.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => roleIds.Contains(rp.RoleId) && !rp.IsDeleted)
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync(ct);

        var permissions = await _db.Permissions
            .IgnoreQueryFilters()
            .Where(p => permissionIds.Contains(p.Id) && !p.IsDeleted)
            .Select(p => p.Code)
            .ToListAsync(ct);

        var companies = await _db.UserCompanies
            .IgnoreQueryFilters()
            .Where(uc => uc.UserId == userId && !uc.IsDeleted)
            .Select(uc => uc.CompanyId)
            .ToListAsync(ct);

        return new UserAuthData(roles, permissions, companies);
    }

    public void Add(User user) => _db.Users.Add(user);
}
