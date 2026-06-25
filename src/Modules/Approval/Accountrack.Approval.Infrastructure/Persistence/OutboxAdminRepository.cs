using Accountrack.Application.Abstractions.Context;
using Accountrack.Approval.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Approval.Infrastructure.Persistence;

/// <summary>
/// Tenant-scoped operator view over the outbox table for dead-letter triage/retry. The table itself
/// carries no global query filter (the dispatcher drains every tenant), so this repository filters by
/// the current tenant explicitly.
/// </summary>
public sealed class OutboxAdminRepository : IOutboxAdminRepository
{
    private readonly ApprovalDbContext _db;
    private readonly ITenantContext _tenant;

    public OutboxAdminRepository(ApprovalDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<DeadLetterMessage>> ListDeadLetteredAsync(int maxAttempts, CancellationToken ct) =>
        await _db.OutboxMessages
            .Where(m => m.TenantId == _tenant.TenantId
                && m.ProcessedOnUtc == null
                && m.Attempts >= maxAttempts)
            .OrderBy(m => m.OccurredOnUtc)
            .Select(m => new DeadLetterMessage(m.Id, m.Type, m.OccurredOnUtc, m.Attempts, m.Error))
            .ToListAsync(ct);

    public async Task<bool> RequeueAsync(Guid id, CancellationToken ct)
    {
        var message = await _db.OutboxMessages.FirstOrDefaultAsync(
            m => m.Id == id && m.TenantId == _tenant.TenantId && m.ProcessedOnUtc == null, ct);
        if (message is null)
        {
            return false;
        }

        message.Attempts = 0;
        message.Error = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
