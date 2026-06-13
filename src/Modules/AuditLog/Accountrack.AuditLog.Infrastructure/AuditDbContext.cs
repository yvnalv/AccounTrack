using Accountrack.Application.Abstractions.Context;
using Accountrack.Infrastructure.Common.Persistence;
using Accountrack.SharedKernel.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.AuditLog.Infrastructure;

/// <summary>
/// Owns the shared <c>audit.AuditEntries</c> table (ADR-0026) — it is the single context that
/// includes the table in its migrations. Other module contexts write to the same table but
/// exclude it from their migrations. Used here for read queries.
/// </summary>
public sealed class AuditDbContext : BaseDbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options, ITenantContext tenant)
        : base(options, tenant)
    {
    }

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AuditSchema);

        // This context owns the table's creation (not excluded from migrations).
        MapAuditEntry(modelBuilder, excludeFromMigrations: false);

        base.OnModelCreating(modelBuilder);
    }
}
