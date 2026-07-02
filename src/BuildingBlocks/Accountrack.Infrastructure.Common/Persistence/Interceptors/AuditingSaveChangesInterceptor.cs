using Accountrack.Application.Abstractions.Context;
using Accountrack.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Accountrack.Infrastructure.Common.Persistence.Interceptors;

/// <summary>
/// Stamps audit fields (IAuditable), tenancy (ITenantOwned), and converts physical deletes into
/// soft deletes (ISoftDeletable) on every save (ADR-0006, MULTI_TENANCY.md §6).
/// Application code never sets these fields directly.
/// </summary>
public sealed class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public AuditingSaveChangesInterceptor(ITenantContext tenant, ICurrentUser user, IClock clock)
    {
        _tenant = tenant;
        _user = user;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var userId = _user.IsAuthenticated ? _user.UserId : Guid.Empty;

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            StampTenancy(entry);

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    // Seed the provider-agnostic concurrency token (ADR-0021). Unlike SQL Server's
                    // store-generated rowversion, PostgreSQL has none, so the app owns the value.
                    entry.Entity.RowVersion = NewConcurrencyToken();
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    // Bump the token so a concurrent stale write fails the WHERE-clause check.
                    entry.Entity.RowVersion = NewConcurrencyToken();
                    break;

                case EntityState.Deleted:
                    // Soft delete instead of physical delete (BR-X-2).
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.DeletedBy = userId;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    entry.Entity.RowVersion = NewConcurrencyToken();
                    break;
            }
        }
    }

    /// <summary>A fresh 16-byte optimistic-concurrency token (opaque; round-tripped to clients).</summary>
    private static byte[] NewConcurrencyToken() => Guid.NewGuid().ToByteArray();

    private void StampTenancy(EntityEntry<Entity> entry)
    {
        if (entry.Entity is not ITenantScoped scoped)
        {
            return;
        }

        if (entry.State == EntityState.Added)
        {
            if (_tenant.IsSet)
            {
                scoped.TenantId = _tenant.TenantId;
                if (scoped is ITenantOwned owned)
                {
                    owned.CompanyId = _tenant.CompanyId;
                }
            }
            else if (scoped.TenantId == Guid.Empty)
            {
                // Allowed only when a TenantId is set explicitly (e.g. startup seeding /
                // reviewed admin paths — MULTI_TENANCY.md §6).
                throw new InvalidOperationException(
                    "Cannot persist a tenant-scoped entity without an established tenant context " +
                    "or an explicit TenantId.");
            }
        }
        else if (entry.State is EntityState.Modified or EntityState.Deleted
                 && _tenant.IsSet
                 && scoped.TenantId != _tenant.TenantId)
        {
            // Defense against detached-entity tampering (MULTI_TENANCY.md §6).
            throw new InvalidOperationException("Tenant mismatch on a tracked entity.");
        }
    }
}
