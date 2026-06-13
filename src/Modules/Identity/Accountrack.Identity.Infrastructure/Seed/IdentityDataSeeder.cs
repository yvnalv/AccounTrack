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
