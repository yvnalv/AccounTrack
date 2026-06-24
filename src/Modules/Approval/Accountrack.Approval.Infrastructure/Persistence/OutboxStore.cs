using System.Text.Json;
using Accountrack.Application.Abstractions.Context;
using Accountrack.Application.Abstractions.Integration;
using Accountrack.Infrastructure.Common.Outbox;
using Accountrack.Modules.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace Accountrack.Approval.Infrastructure.Persistence;

/// <summary>
/// Outbox writer + reader backed by the Approval context, so an <see cref="Enqueue"/> commits in the
/// same transaction as the approval change (transactional outbox — ADR-0007). The dispatcher reads
/// pending rows through the same store.
/// </summary>
public sealed class OutboxStore : IOutbox, IOutboxStore
{
    private readonly ApprovalDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public OutboxStore(ApprovalDbContext db, ITenantContext tenant, IClock clock)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
    }

    public void Enqueue(IIntegrationEvent integrationEvent)
    {
        var message = new OutboxMessage
        {
            TenantId = _tenant.TenantId,
            CompanyId = _tenant.CompanyId,
            Type = integrationEvent.GetType().AssemblyQualifiedName!,
            Content = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredOnUtc = integrationEvent.OccurredAtUtc,
        };

        _db.OutboxMessages.Add(message);
    }

    public async Task<IReadOnlyList<OutboxRecord>> GetPendingAsync(int max, int maxAttempts, CancellationToken ct) =>
        await _db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && m.Attempts < maxAttempts)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(max)
            .Select(m => new OutboxRecord(m.Id, m.TenantId, m.CompanyId, m.Type, m.Content))
            .ToListAsync(ct);

    public async Task MarkProcessedAsync(Guid id, CancellationToken ct)
    {
        var message = await _db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (message is null)
        {
            return;
        }

        message.ProcessedOnUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordFailureAsync(Guid id, string error, CancellationToken ct)
    {
        var message = await _db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (message is null)
        {
            return;
        }

        message.Attempts++;
        message.Error = error.Length > 2000 ? error[..2000] : error;
        await _db.SaveChangesAsync(ct);
    }
}
