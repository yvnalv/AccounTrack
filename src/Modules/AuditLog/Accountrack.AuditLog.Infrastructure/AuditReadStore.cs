using Accountrack.AuditLog.Application;
using Accountrack.Application.Abstractions.Context;
using Accountrack.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.AuditLog.Infrastructure;

/// <summary>Reads the append-only audit store, scoped to the current tenant (MULTI_TENANCY.md).</summary>
public sealed class AuditReadStore : IAuditReadStore
{
    private readonly AuditDbContext _db;
    private readonly ITenantContext _tenant;

    public AuditReadStore(AuditDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<PagedResult<AuditEntryDto>> QueryAsync(AuditEntryFilter filter, CancellationToken ct)
    {
        var query = _db.AuditEntries
            .AsNoTracking()
            .Where(e => e.TenantId == _tenant.TenantId);

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(e => e.EntityType == filter.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            query = query.Where(e => e.EntityId == filter.EntityId);
        }

        if (filter.FromUtc is { } from)
        {
            query = query.Where(e => e.TimestampUtc >= from);
        }

        if (filter.ToUtc is { } to)
        {
            query = query.Where(e => e.TimestampUtc <= to);
        }

        var total = await query.LongCountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.TimestampUtc)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(e => new AuditEntryDto(
                e.Id,
                e.TenantId,
                e.CompanyId,
                e.EntityType,
                e.EntityId,
                e.Action.ToString(),
                e.ChangesJson,
                e.UserId,
                e.TimestampUtc))
            .ToListAsync(ct);

        return new PagedResult<AuditEntryDto>(items, filter.Page, filter.PageSize, total);
    }
}
