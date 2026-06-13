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
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;

                case EntityState.Deleted:
                    // Soft delete instead of physical delete (BR-X-2).
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.DeletedBy = userId;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }
    }

    private void StampTenancy(EntityEntry<Entity> entry)
    {
        if (entry.Entity is not ITenantOwned owned)
        {
            return;
        }

        if (entry.State == EntityState.Added)
        {
            if (!_tenant.IsSet)
            {
                throw new InvalidOperationException(
                    "Cannot persist a tenant-owned entity without an established tenant context.");
            }

            owned.TenantId = _tenant.TenantId;
            owned.CompanyId = _tenant.CompanyId;
        }
        else if (entry.State is EntityState.Modified or EntityState.Deleted && _tenant.IsSet)
        {
            // Defense against detached-entity tampering (MULTI_TENANCY.md §6).
            if (owned.TenantId != _tenant.TenantId)
            {
                throw new InvalidOperationException("Tenant mismatch on a tracked entity.");
            }
        }
    }
}
