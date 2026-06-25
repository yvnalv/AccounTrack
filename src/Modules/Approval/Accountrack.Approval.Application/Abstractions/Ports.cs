using Accountrack.Approval.Domain;

namespace Accountrack.Approval.Application.Abstractions;

public interface IApprovalDefinitionRepository
{
    void Add(ApprovalDefinition definition);
    Task<IReadOnlyList<ApprovalDefinition>> GetActiveForDocumentTypeAsync(string documentType, CancellationToken ct);
    Task<IReadOnlyList<ApprovalDefinition>> ListAsync(CancellationToken ct);
}

public interface IApprovalRequestRepository
{
    void Add(ApprovalRequest request);
    Task<ApprovalRequest?> GetAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsForDocumentAsync(string documentType, Guid documentId, CancellationToken ct);
    Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(CancellationToken ct);
}

public interface IApprovalUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}

/// <summary>A dead-lettered outbox message (Type is the assembly-qualified event type).</summary>
public sealed record DeadLetterMessage(Guid Id, string Type, DateTime OccurredOnUtc, int Attempts, string? Error);

/// <summary>
/// Operator view over the transactional outbox: lists messages the dispatcher has given up on (at or
/// beyond the attempt cap) for the current tenant, and requeues one for redelivery. Tenant-scoped in
/// the implementation — the outbox table itself is unfiltered so the background dispatcher can drain
/// every tenant.
/// </summary>
public interface IOutboxAdminRepository
{
    Task<IReadOnlyList<DeadLetterMessage>> ListDeadLetteredAsync(int maxAttempts, CancellationToken ct);
    Task<bool> RequeueAsync(Guid id, CancellationToken ct);
}
