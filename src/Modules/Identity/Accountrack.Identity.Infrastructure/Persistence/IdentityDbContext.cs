using Accountrack.Application.Abstractions.Context;
using Accountrack.Identity.Application.Abstractions;
using Accountrack.Identity.Domain;
using Accountrack.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core context owning the Identity module's schema ("identity"). Inherits the platform
/// conventions (tenant/soft-delete query filters, audit stamping, rowversion) from
/// <see cref="BaseDbContext"/> and serves as the module's unit of work.
/// </summary>
public sealed class IdentityDbContext : BaseDbContext, IIdentityUnitOfWork
{
    public const string Schema = "identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        base.OnModelCreating(modelBuilder);

        // Apply tenant/soft-delete filters, rowversion, and event-buffer ignore to all entities.
        ApplyAccountrackConventions(modelBuilder);
    }
}
