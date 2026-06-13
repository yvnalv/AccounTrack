using System.Text.Json;
using Accountrack.Application.Abstractions.Context;
using Accountrack.SharedKernel.Auditing;
using Accountrack.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Accountrack.Infrastructure.Common.Persistence.Interceptors;

/// <summary>
/// Captures an immutable <see cref="AuditEntry"/> for every inserted/updated/deleted business
/// entity and persists it in the SAME transaction as the change (ADR-0006/0026, SECURITY.md §4).
/// Must run BEFORE <see cref="AuditingSaveChangesInterceptor"/> so deletes are seen as deletes
/// before they are rewritten to soft-delete updates.
/// </summary>
public sealed class AuditCaptureInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Never audit these properties (noise / not business-meaningful).
    private static readonly HashSet<string> IgnoredProperties = new(StringComparer.Ordinal)
    {
        nameof(Entity.RowVersion),
        nameof(Entity.CreatedAt), nameof(Entity.CreatedBy),
        nameof(Entity.UpdatedAt), nameof(Entity.UpdatedBy),
    };

    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public AuditCaptureInterceptor(ITenantContext tenant, ICurrentUser user, IClock clock)
    {
        _tenant = tenant;
        _user = user;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var userId = _user.IsAuthenticated ? _user.UserId : Guid.Empty;

        // Snapshot the list first — we mutate the change tracker by adding audit entries.
        var entries = context.ChangeTracker.Entries<Entity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Insert,
                EntityState.Deleted => AuditAction.Delete,
                _ => AuditAction.Update,
            };

            var changes = BuildChanges(entry, action);
            if (changes is null)
            {
                continue; // nothing meaningful changed
            }

            var (tenantId, companyId) = ResolveOwnership(entry.Entity);

            context.Set<AuditEntry>().Add(new AuditEntry(
                tenantId,
                companyId,
                entry.Entity.GetType().Name,
                entry.Entity.Id.ToString(),
                action,
                JsonSerializer.Serialize(changes, JsonOptions),
                userId,
                now,
                correlationId: null));
        }
    }

    private static Dictionary<string, object?>? BuildChanges(EntityEntry<Entity> entry, AuditAction action)
    {
        var result = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            if (IgnoredProperties.Contains(name))
            {
                continue;
            }

            switch (action)
            {
                case AuditAction.Insert:
                    result[name] = prop.CurrentValue;
                    break;

                case AuditAction.Delete:
                    result[name] = prop.OriginalValue;
                    break;

                default: // Update — only changed properties
                    if (prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue))
                    {
                        result[name] = new { old = prop.OriginalValue, @new = prop.CurrentValue };
                    }

                    break;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private (Guid TenantId, Guid? CompanyId) ResolveOwnership(Entity entity)
    {
        var tenantId = entity switch
        {
            ITenantScoped scoped when scoped.TenantId != Guid.Empty => scoped.TenantId,
            _ => _tenant.IsSet ? _tenant.TenantId : Guid.Empty,
        };

        Guid? companyId = entity is ITenantOwned owned ? owned.CompanyId : null;
        return (tenantId, companyId);
    }
}
