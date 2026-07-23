using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Domain;
using Accountrack.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Identity.Infrastructure.Seed;

/// <summary>Settings for the optional development bootstrap admin.</summary>
public sealed class IdentitySeedSettings
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }

    public string AdminEmail { get; set; } = "admin@accountrack.local";

    public string AdminPassword { get; set; } = "ChangeMe!123";
}

/// <summary>
/// Seeds the global permission catalog and, when enabled, a development tenant + administrator
/// so the platform is usable before the Company module manages tenants. All lookups bypass the
/// tenant filter because seeding runs without a tenant context.
/// </summary>
public static class IdentityDataSeeder
{
    // Well-known development tenant/company ids (replaced by real Company-module data later).
    public static readonly Guid DevTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DevCompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task SeedAsync(
        IdentityDbContext db,
        IPasswordHasher passwordHasher,
        IdentitySeedSettings settings,
        CancellationToken ct = default)
    {
        await SeedPermissionsAsync(db, ct);

        if (settings.Enabled)
        {
            await SeedDevAdminAsync(db, passwordHasher, settings, ct);
            // Seed the standard non-admin roles for the dev tenant (idempotent).
            await EnsureStandardRolesAsync(db, DevTenantId, ct);
        }

        // Keep EVERY tenant's Administrator role current as new permissions are added to the catalog
        // (BR-SEC-4). Runs unconditionally — including in production where dev seeding is off — so a
        // permission introduced after an organization signed up still reaches its Administrator. Without
        // this, a self-registered org's admin gets a 403 on any newly-shipped feature (e.g. Billing).
        await EnsureAdminHasAllPermissionsAsync(db, ct);
    }

    /// <summary>Creates any missing standard non-admin roles (Accountant, Sales, …) for a tenant.</summary>
    private static async Task EnsureStandardRolesAsync(IdentityDbContext db, Guid tenantId, CancellationToken ct)
    {
        var existingNames = await db.Roles
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Select(r => r.Name)
            .ToListAsync(ct);

        var permissionIdByCode = await db.Permissions
            .IgnoreQueryFilters()
            .ToDictionaryAsync(p => p.Code, p => p.Id, ct);

        foreach (var template in StandardRoleDefinitions.NonAdminTemplates)
        {
            if (existingNames.Contains(template.Name))
            {
                continue;
            }

            var role = new Role(tenantId, template.Name, template.Description, isSystem: true);
            foreach (var code in template.Permissions)
            {
                if (permissionIdByCode.TryGetValue(code, out var pid))
                {
                    role.GrantPermission(pid);
                }
            }

            db.Roles.Add(role);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Grants any catalog permissions the system Administrator role is missing (e.g. permissions
    /// added in a later release), so the admin stays fully privileged across upgrades.
    /// </summary>
    /// <summary>
    /// Grants every catalog permission to the <b>Administrator</b> role of <b>every tenant</b> (BR-SEC-4):
    /// the dev tenant and every self-registered organization. The invariant "an Administrator always holds
    /// the full permission catalog" must survive permission additions, so this backfills the grants missing
    /// from each admin role. Idempotent — only missing <c>(role, permission)</c> rows are inserted (a role
    /// already complete is untouched, so it is safe to run on every startup). Cross-tenant admin write,
    /// startup-only (Rule 33). Rows are inserted directly rather than via the Role aggregate to avoid an
    /// optimistic-concurrency bump on the unchanged Role.
    /// </summary>
    private static async Task EnsureAdminHasAllPermissionsAsync(IdentityDbContext db, CancellationToken ct)
    {
        var adminRoleIds = await db.Roles
            .IgnoreQueryFilters()
            .Where(r => r.Name == SystemRoles.Administrator && r.IsSystem && !r.IsDeleted)
            .Select(r => r.Id)
            .ToListAsync(ct);
        if (adminRoleIds.Count == 0)
        {
            return;
        }

        var allPermissionIds = await db.Permissions.IgnoreQueryFilters().Select(p => p.Id).ToListAsync(ct);

        // Grants that already exist for these admin roles, grouped per role.
        var existing = await db.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => adminRoleIds.Contains(rp.RoleId) && !rp.IsDeleted)
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(ct);
        var grantedByRole = existing
            .GroupBy(g => g.RoleId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PermissionId).ToHashSet());

        var inserted = false;
        foreach (var roleId in adminRoleIds)
        {
            var have = grantedByRole.TryGetValue(roleId, out var set) ? set : new HashSet<Guid>();
            foreach (var permissionId in allPermissionIds)
            {
                if (have.Add(permissionId))
                {
                    db.RolePermissions.Add(new RolePermission(roleId, permissionId));
                    inserted = true;
                }
            }
        }

        if (inserted)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedPermissionsAsync(IdentityDbContext db, CancellationToken ct)
    {
        var existing = await db.Permissions
            .IgnoreQueryFilters()
            .Select(p => p.Code)
            .ToListAsync(ct);

        var missing = PermissionCatalog.All
            .Where(p => !existing.Contains(p.Code))
            .Select(p => new Permission(p.Code, p.Name));

        await db.Permissions.AddRangeAsync(missing, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedDevAdminAsync(
        IdentityDbContext db,
        IPasswordHasher passwordHasher,
        IdentitySeedSettings settings,
        CancellationToken ct)
    {
        var anyUser = await db.Users.IgnoreQueryFilters().AnyAsync(ct);
        if (anyUser)
        {
            return;
        }

        var allPermissionIds = await db.Permissions
            .IgnoreQueryFilters()
            .Select(p => p.Id)
            .ToListAsync(ct);

        var adminRole = new Role(DevTenantId, SystemRoles.Administrator, "Full access", isSystem: true);
        foreach (var permissionId in allPermissionIds)
        {
            adminRole.GrantPermission(permissionId);
        }

        db.Roles.Add(adminRole);

        var admin = User.Create(
            DevTenantId,
            Email.Create(settings.AdminEmail),
            passwordHasher.Hash(settings.AdminPassword),
            "Administrator");

        admin.AssignRole(adminRole.Id);
        admin.GrantCompany(DevCompanyId);

        db.Users.Add(admin);

        await db.SaveChangesAsync(ct);
    }
}
